using System.IO.Compression;
using System.Text;
using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Export;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Audit;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Features.PayrollReports;
using smartHRMS.Application.Features.PayrollReports.Dtos;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.PayrollReports;

public class PayrollReportServiceTests
{
    private sealed class Setup
    {
        public FakeEmployeeRepository Employees { get; } = new();
        public FakePayrollRepository Payroll { get; }
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = FakeCurrentUser.As(UserRole.HR);
        public AttendanceClock Clock { get; } = new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 6, 0, 0, TimeSpan.Zero)), new AttendanceOptions());
        public PayrollService Payrolls { get; }
        public PayslipService Payslips { get; }
        public PayrollReportService Reports { get; }
        public Department Ops { get; } = new() { Name = "Ops" };
        public Department Finance { get; } = new() { Name = "Finance" };
        public Employee Alice { get; }
        public Employee Bob { get; }

        public Setup()
        {
            Payroll = new FakePayrollRepository(Employees);
            Alice = Add("EMP-001", "Alice", 21000, Ops, new EmployeeSalaryStructure { HouseRent = 10500, MonthlyTax = 1000 }); // gross 31500, net 30500
            Bob = Add("EMP-002", "Bob", 42000, Finance, null); // gross = net 42000
            var users = new FakeUserRepository();
            Payrolls = new PayrollService(Payroll, Employees, new FakeAttendanceRepository(Employees), new FakeLeaveRequestRepository(Employees), users, Audit, User,
                new WorkCalendar(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] }), new PayrollOptions(), Clock);
            Payslips = new PayslipService(Payroll, Employees, users, Audit, User, new PayrollOptions(), Clock);
            Reports = new PayrollReportService(new FakePayrollReportRepository(Payroll, Employees), Audit, User);
        }

        private Employee Add(string code, string name, decimal basic, Department department, EmployeeSalaryStructure? structure)
        {
            var employee = new Employee
            {
                EmployeeCode = code, FirstName = name, LastName = "Test", Status = EmployeeStatus.Active, JoiningDate = new DateTime(2020, 1, 1),
                BasicSalary = basic, Department = department, DepartmentId = department.Id, Designation = new Designation { Name = "Clerk" },
            };
            if (structure is not null)
            {
                structure.EmployeeId = employee.Id;
                employee.SalaryStructure = structure;
            }

            Employees.Employees.Add(employee);
            return employee;
        }

        /// <summary>October 2026 approved and finalized (issued payroll); November 2026 only calculated.</summary>
        public async Task<(Guid October, Guid November)> SeedAsync()
        {
            User.SignInAs(UserRole.HR);
            var october = await Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31) }, CancellationToken.None);
            await Payrolls.CalculateAsync(october.Id, CancellationToken.None);
            await Payrolls.SubmitAsync(october.Id, CancellationToken.None);
            User.SignInAs(UserRole.Admin);
            await Payrolls.ApproveAsync(october.Id, CancellationToken.None);
            await Payrolls.FinalizeAsync(october.Id, CancellationToken.None);
            User.SignInAs(UserRole.HR);
            var november = await Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = new DateOnly(2026, 11, 1), EndDate = new DateOnly(2026, 11, 30) }, CancellationToken.None);
            await Payrolls.CalculateAsync(november.Id, CancellationToken.None);
            Audit.Entries.Clear();
            return (october.Id, november.Id);
        }

        public PayrollRecord RecordOf(Employee employee, Guid periodId) => Payroll.Records.Single(r => r.EmployeeId == employee.Id && r.PayrollPeriodId == periodId);
    }

    private static string Text(ExportFile file) => Encoding.UTF8.GetString(file.Content);

    // ---- reports ----

    [Fact]
    public async Task Summary_IncludesOnlyIssuedPayrollByDefault_WithCorrectTotals()
    {
        var s = new Setup();
        var (october, november) = await s.SeedAsync();

        var report = await s.Reports.GetReportAsync(new PayrollReportQueryDto(), PayrollReportGroupBy.Period, CancellationToken.None);

        Assert.Equal(new[] { "Approved", "Finalized", "Paid" }, report.Statuses);
        var row = Assert.Single(report.Rows);
        Assert.Equal(october, row.PayrollPeriodId);
        Assert.Equal("Finalized", row.PeriodStatus);
        var t = report.Totals;
        Assert.Equal(2, t.EmployeeCount);
        Assert.Equal(2, t.RecordCount);
        Assert.Equal(63000m, t.BasicSalary);
        Assert.Equal(10500m, t.TotalAllowances);
        Assert.Equal(73500m, t.GrossSalary);
        Assert.Equal(1000m, t.Tax);
        Assert.Equal(0m, t.OtherDeductions);
        Assert.Equal(1000m, t.TotalDeduction);
        Assert.Equal(72500m, t.NetSalary);
        Assert.Equal(0m, t.PaidNetSalary);
        Assert.Equal(72500m, t.UnpaidNetSalary);

        var draft = await s.Reports.GetReportAsync(new PayrollReportQueryDto { Status = "Calculated" }, PayrollReportGroupBy.Period, CancellationToken.None);
        Assert.Equal(november, Assert.Single(draft.Rows).PayrollPeriodId);
    }

    [Fact]
    public async Task Totals_MatchTheStoredRecords_AcrossPeriods_AndCountEmployeesOnce()
    {
        var s = new Setup();
        await s.SeedAsync();

        var all = await s.Reports.GetReportAsync(new PayrollReportQueryDto { Year = 2026 }, PayrollReportGroupBy.Period, CancellationToken.None);
        var anyStatus = await s.Reports.GetReportAsync(new PayrollReportQueryDto { Status = "Calculated", Year = 2026 }, PayrollReportGroupBy.Period, CancellationToken.None);

        Assert.Equal(s.Payroll.Records.Where(r => r.PayrollPeriod!.Status == PayrollPeriodStatus.Finalized).Sum(r => r.NetSalary), all.Totals.NetSalary);
        Assert.Equal(2, anyStatus.Totals.EmployeeCount); // the same two employees, not 4 records
    }

    [Fact]
    public async Task DepartmentReport_GroupsByDepartment()
    {
        var s = new Setup();
        await s.SeedAsync();

        var report = await s.Reports.GetReportAsync(new PayrollReportQueryDto(), PayrollReportGroupBy.Department, CancellationToken.None);

        Assert.Equal("department", report.GroupBy);
        Assert.Equal(new[] { "Finance", "Ops" }, report.Rows.Select(r => r.Label));
        Assert.Equal(42000m, report.Rows[0].NetSalary);
        Assert.Equal(1, report.Rows[1].EmployeeCount);
        Assert.Equal(1000m, report.Rows[1].TotalDeduction);
    }

    [Fact]
    public async Task Filters_DepartmentEmployeeMonthPeriodAndDateRange()
    {
        var s = new Setup();
        var (october, _) = await s.SeedAsync();

        Assert.Equal(30500m, (await Summary(s, new PayrollReportQueryDto { DepartmentId = s.Ops.Id })).NetSalary);
        Assert.Equal(42000m, (await Summary(s, new PayrollReportQueryDto { EmployeeId = s.Bob.Id })).NetSalary);
        Assert.Equal(72500m, (await Summary(s, new PayrollReportQueryDto { Month = 10, Year = 2026, PayrollPeriodId = october })).NetSalary);
        Assert.Equal(0, (await Summary(s, new PayrollReportQueryDto { Month = 9 })).RecordCount);
        Assert.Equal(2, (await Summary(s, new PayrollReportQueryDto { From = new DateOnly(2026, 10, 31), To = new DateOnly(2026, 10, 31) })).RecordCount);
        Assert.Equal(0, (await Summary(s, new PayrollReportQueryDto { From = new DateOnly(2026, 11, 1) })).RecordCount);
    }

    private static async Task<PayrollAmountsDto> Summary(Setup s, PayrollReportQueryDto query) =>
        (await s.Reports.GetReportAsync(query, PayrollReportGroupBy.Period, CancellationToken.None)).Totals;

    [Fact]
    public async Task PaidAmounts_FollowThePayslipPaymentStatus()
    {
        var s = new Setup();
        var (october, _) = await s.SeedAsync();
        s.RecordOf(s.Alice, october).Payslip!.PaymentStatus = PaymentStatus.Paid;

        var totals = await Summary(s, new PayrollReportQueryDto());

        Assert.Equal(30500m, totals.PaidNetSalary);
        Assert.Equal(42000m, totals.UnpaidNetSalary);
    }

    [Theory]
    [InlineData(null, "period")]
    [InlineData("department", "department")]
    [InlineData("Period", "period")]
    public async Task DeductionAndAllowanceReports_GroupByPeriodOrDepartment(string? groupBy, string expected)
    {
        var s = new Setup();
        await s.SeedAsync();

        var report = await s.Reports.GetReportAsync(new PayrollReportQueryDto { GroupBy = groupBy }, PayrollReportGroupBy.Period, CancellationToken.None);

        Assert.Equal(expected, report.GroupBy);
    }

    [Theory]
    [InlineData(13, null, null, null, null)]
    [InlineData(null, 1999, null, null, null)]
    [InlineData(null, null, "Done", null, null)]
    [InlineData(null, null, null, "employee", null)]
    [InlineData(null, null, null, null, "2026-10-01")]
    public async Task InvalidReportFilters_AreRejected(int? month, int? year, string? status, string? groupBy, string? fromAfterTo)
    {
        var s = new Setup();
        var query = new PayrollReportQueryDto { Month = month, Year = year, Status = status, GroupBy = groupBy };
        if (fromAfterTo is not null)
        {
            query.From = DateOnly.Parse(fromAfterTo);
            query.To = query.From.Value.AddDays(-1);
        }

        await Assert.ThrowsAsync<BadRequestException>(() => s.Reports.GetReportAsync(query, PayrollReportGroupBy.Period, CancellationToken.None));
    }

    [Fact]
    public async Task EmployeeReport_IsPagedAndFiltered_WithServerComputedTotals()
    {
        var s = new Setup();
        await s.SeedAsync();

        var page = await s.Reports.GetEmployeeReportAsync(new PayrollReportQueryDto { PageSize = 1 }, CancellationToken.None);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        var alice = Assert.Single(page.Items);
        Assert.Equal("EMP-001", alice.EmployeeCode);
        Assert.Equal(10500m, alice.TotalAllowances);
        Assert.Equal(0m, alice.OtherDeductions);
        Assert.False(alice.CanEdit); // finalized payroll is read-only

        var search = await s.Reports.GetEmployeeReportAsync(new PayrollReportQueryDto { Search = "Bob" }, CancellationToken.None);
        Assert.Equal("EMP-002", Assert.Single(search.Items).EmployeeCode);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Reports.GetEmployeeReportAsync(new PayrollReportQueryDto { PageSize = 101 }, CancellationToken.None));
    }

    [Fact]
    public async Task PeriodHistory_ListsEveryStatus_WithTotalsAndDates()
    {
        var s = new Setup();
        var (october, november) = await s.SeedAsync();

        var page = await s.Reports.GetPeriodHistoryAsync(new PayrollPeriodHistoryQueryDto(), CancellationToken.None);
        Assert.Equal(new[] { november, october }, page.Items.Select(p => p.Id));
        var oct = page.Items[1];
        Assert.Equal("Finalized", oct.Status);
        Assert.True(oct.IsLocked);
        Assert.Equal((10, 2026), (oct.Month, oct.Year));
        Assert.NotNull(oct.CalculatedAt);
        Assert.NotNull(oct.FinalizedAt);
        Assert.Equal(72500m, oct.NetSalary);
        Assert.Equal(1000m, oct.Tax);
        Assert.Null(page.Items[0].FinalizedAt);

        Assert.Equal(november, Assert.Single((await s.Reports.GetPeriodHistoryAsync(new PayrollPeriodHistoryQueryDto { Status = "Calculated" }, CancellationToken.None)).Items).Id);
        Assert.Equal(october, Assert.Single((await s.Reports.GetPeriodHistoryAsync(new PayrollPeriodHistoryQueryDto { Month = 10, Year = 2026 }, CancellationToken.None)).Items).Id);
        Assert.Equal(november, Assert.Single((await s.Reports.GetPeriodHistoryAsync(new PayrollPeriodHistoryQueryDto { Search = "November" }, CancellationToken.None)).Items).Id);
        Assert.Single((await s.Reports.GetPeriodHistoryAsync(new PayrollPeriodHistoryQueryDto { PageSize = 1 }, CancellationToken.None)).Items);
    }

    // ---- authorization ----

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Manager)]
    public async Task OnlyHrAndAdmin_ReadOrExportReports(UserRole role)
    {
        var s = new Setup();
        await s.SeedAsync();
        s.User.SignInAs(role, s.Alice.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Reports.GetReportAsync(new PayrollReportQueryDto { EmployeeId = s.Alice.Id }, PayrollReportGroupBy.Period, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Reports.GetEmployeeReportAsync(new PayrollReportQueryDto { EmployeeId = s.Alice.Id }, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Reports.GetPeriodHistoryAsync(new PayrollPeriodHistoryQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Reports.ExportAsync(PayrollReportKind.Summary, new PayrollReportQueryDto(), "csv", CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payslips.ExportAsync(new PayrollHistoryQueryDto(), mine: false, "csv", CancellationToken.None));
        Assert.DoesNotContain(s.Audit.Entries, e => e.Action.EndsWith("Exported"));
    }

    [Fact]
    public async Task Admin_ReadsReports()
    {
        var s = new Setup();
        await s.SeedAsync();
        s.User.SignInAs(UserRole.Admin);

        Assert.Equal(72500m, (await Summary(s, new PayrollReportQueryDto())).NetSalary);
    }

    // ---- export ----

    [Fact]
    public async Task ExportSummary_Csv_HasHeaderRowsAndTotal_AndIsAuditedWithoutAmounts()
    {
        var s = new Setup();
        await s.SeedAsync();

        var file = await s.Reports.ExportAsync(PayrollReportKind.Summary, new PayrollReportQueryDto { Year = 2026 }, "csv", CancellationToken.None);

        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("payroll-summary-", file.FileName);
        Assert.EndsWith(".csv", file.FileName);
        var lines = Text(file).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Payroll period,Status,Employees,Basic salary,Allowances,Overtime,Bonus,Gross salary,Tax", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Total,,2,63000.00,10500.00,0.00,0.00,73500.00,1000.00,0.00,1000.00,72500.00,0.00,72500.00", lines[2]);

        var entry = Assert.Single(s.Audit.Entries);
        Assert.Equal(AuditActions.PayrollReportExported, entry.Action);
        Assert.Contains("Summary report exported as csv (2 rows); year=2026", entry.Details);
        Assert.DoesNotContain("72500", entry.Details);
    }

    [Theory]
    [InlineData(PayrollReportKind.Periods, "Payroll period,Start,End,Status")]
    [InlineData(PayrollReportKind.Departments, "Department,Employees,Basic salary")]
    [InlineData(PayrollReportKind.Employees, "Employee ID,Employee,Department,Designation,Payroll period")]
    [InlineData(PayrollReportKind.Deductions, "Payroll period,Employees,Tax,Provident fund")]
    [InlineData(PayrollReportKind.Allowances, "Payroll period,Employees,House rent,Medical")]
    public async Task EveryReport_ExportsWithItsColumns(PayrollReportKind kind, string header)
    {
        var s = new Setup();
        await s.SeedAsync();

        var csv = Text(await s.Reports.ExportAsync(kind, new PayrollReportQueryDto(), null, CancellationToken.None));

        Assert.StartsWith(header, csv.TrimStart('﻿'));
    }

    [Fact]
    public async Task ExportXlsx_IsAValidWorkbook()
    {
        var s = new Setup();
        await s.SeedAsync();

        var file = await s.Reports.ExportAsync(PayrollReportKind.Departments, new PayrollReportQueryDto(), "xlsx", CancellationToken.None);

        Assert.EndsWith(".xlsx", file.FileName);
        using var zip = new ZipArchive(new MemoryStream(file.Content));
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = reader.ReadToEnd();
        Assert.Contains("<t xml:space=\"preserve\">Finance</t>", sheet);
        Assert.Contains("<v>72500</v>", sheet.Replace("<v>72500.00</v>", "<v>72500</v>"));
    }

    [Fact]
    public async Task UnknownExportFormat_IsRejected()
    {
        var s = new Setup();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Reports.ExportAsync(PayrollReportKind.Summary, new PayrollReportQueryDto(), "pdf", CancellationToken.None));
    }

    [Fact]
    public void Csv_GuardsAgainstFormulaInjection_AndQuotes()
    {
        var table = new ExportTable("t", new ExportColumn("Name"), new ExportColumn("Amount", ExportKind.Amount)).Add("=HYPERLINK(\"x\")", 1.5m).Add("a, \"b\"", null);

        var csv = Encoding.UTF8.GetString(TableExporter.WriteCsv(table)).TrimStart('﻿');

        Assert.Equal("Name,Amount\r\n\"'=HYPERLINK(\"\"x\"\")\",1.50\r\n\"a, \"\"b\"\"\",\r\n", csv);
    }

    // ---- payslip export ----

    [Fact]
    public async Task PayslipExport_Mine_ContainsOnlyOwnPayslips_EvenWithAnotherEmployeeId()
    {
        var s = new Setup();
        await s.SeedAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var csv = Text(await s.Payslips.ExportAsync(new PayrollHistoryQueryDto { EmployeeId = s.Bob.Id }, mine: true, "csv", CancellationToken.None));

        Assert.Contains("EMP-001", csv);
        Assert.DoesNotContain("EMP-002", csv);
        Assert.Contains(s.Audit.Entries, e => e.Action == AuditActions.PayslipsExported && e.Details!.Contains("(own payslips)"));
    }

    [Fact]
    public async Task PayslipExport_Hr_UsesTheListFilters()
    {
        var s = new Setup();
        var (october, _) = await s.SeedAsync();

        var all = Text(await s.Payslips.ExportAsync(new PayrollHistoryQueryDto { PayrollPeriodId = october }, mine: false, "csv", CancellationToken.None));
        var finance = Text(await s.Payslips.ExportAsync(new PayrollHistoryQueryDto { DepartmentId = s.Finance.Id }, mine: false, "csv", CancellationToken.None));

        Assert.Contains("EMP-001", all);
        Assert.Contains("EMP-002", all);
        Assert.DoesNotContain("EMP-001", finance);
    }

    [Fact]
    public async Task PayrollHistory_FiltersByPeriodDesignationAndRecordStatus()
    {
        var s = new Setup();
        var (october, november) = await s.SeedAsync();

        var byPeriod = await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { PayrollPeriodId = november }, CancellationToken.None);
        Assert.Equal(2, byPeriod.TotalCount);
        Assert.All(byPeriod.Items, r => Assert.Equal(november, r.PayrollPeriodId));
        var finalized = await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { RecordStatus = "Finalized" }, CancellationToken.None);
        Assert.All(finalized.Items, r => Assert.Equal(october, r.PayrollPeriodId));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { RecordStatus = "Nope" }, CancellationToken.None));
    }

    // ---- audit trail ----

    private sealed class CapturingReader : IAuditLogReader
    {
        public AuditLogFilter? Filter { get; private set; }

        public Task<List<AuditLogDto>> SearchAsync(AuditLogFilter filter, CancellationToken cancellationToken)
        {
            Filter = filter;
            return Task.FromResult(new List<AuditLogDto>());
        }
    }

    [Fact]
    public async Task AuditLog_PayrollCategory_AndOfficeDateRange()
    {
        var reader = new CapturingReader();
        var user = FakeCurrentUser.As(UserRole.Admin);
        var service = new AuditLogService(reader, user, new Setup().Clock);

        await service.SearchAsync(new AuditLogQueryDto { Category = "payroll", From = new DateOnly(2026, 10, 5), To = new DateOnly(2026, 10, 5) }, CancellationToken.None);

        Assert.Equal(AuditLogService.PayrollEntityTypes, reader.Filter!.EntityTypes);
        Assert.Equal(new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc), reader.Filter.FromUtc); // midnight in Dhaka (UTC+6)
        Assert.Equal(new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc), reader.Filter.ToUtc);
        await Assert.ThrowsAsync<BadRequestException>(() => service.SearchAsync(new AuditLogQueryDto { Category = "leave" }, CancellationToken.None));

        user.SignInAs(UserRole.HR);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SearchAsync(new AuditLogQueryDto { Category = "payroll" }, CancellationToken.None));
    }

    [Fact]
    public async Task WorkflowAudit_RecordsStatusTransitions()
    {
        var s = new Setup();
        await s.SeedAsync();
        s.User.SignInAs(UserRole.HR);
        var period = await s.Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = new DateOnly(2026, 12, 1), EndDate = new DateOnly(2026, 12, 31) }, CancellationToken.None);
        await s.Payrolls.CalculateAsync(period.Id, CancellationToken.None);
        await s.Payrolls.SubmitAsync(period.Id, CancellationToken.None);
        await s.Payrolls.CancelAsync(period.Id, new CancelPayrollDto { Reason = "Wrong month" }, CancellationToken.None);

        var details = s.Audit.Entries.Where(e => e.EntityId == period.Id).Select(e => $"{e.Action}: {e.Details}").ToList();
        Assert.Contains(details, d => d.StartsWith("PayrollPeriodCreated: → Draft"));
        Assert.Contains(details, d => d.StartsWith("PayrollCalculated: Draft → Calculated"));
        Assert.Contains("PayrollSubmitted: Calculated → PendingApproval.", details);
        Assert.Contains("PayrollCancelled: PendingApproval → Cancelled: Wrong month", details);
    }
}
