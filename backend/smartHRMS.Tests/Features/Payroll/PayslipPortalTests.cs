using System.Text;
using System.Text.RegularExpressions;
using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Export;
using smartHRMS.Application.Common.Security;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Application.Features.PayrollReports;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Payroll;

/// <summary>Day 20: employee payroll portal, finalized-only visibility, payslip PDF, print and download auditing.</summary>
public class PayslipPortalTests
{
    private sealed class Setup
    {
        public FakeEmployeeRepository Employees { get; } = new();
        public FakePayrollRepository Payroll { get; }
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = FakeCurrentUser.As(UserRole.HR);
        public PayrollService Payrolls { get; }
        public PayslipService Payslips { get; }
        public PayrollReportService Reports { get; }
        public Employee Alice { get; }
        public Employee Bob { get; }

        public Setup()
        {
            Payroll = new FakePayrollRepository(Employees);
            Alice = Add("EMP-001", "Alice (Ops)", 21000, new EmployeeSalaryStructure { HouseRent = 10500, MonthlyTax = 1000, MonthlyProvidentFund = 2100 }); // net 28,400
            Bob = Add("EMP-002", "Bob", 42000, null); // no deductions
            var users = new FakeUserRepository();
            var clock = new AttendanceClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 6, 0, 0, TimeSpan.Zero)), new AttendanceOptions());
            var options = new PayrollOptions { CompanyName = "Test Co", CompanyAddress = "Dhaka" };
            Payrolls = new PayrollService(Payroll, Employees, new FakeAttendanceRepository(Employees), new FakeLeaveRequestRepository(Employees), users, Audit, User,
                new WorkCalendar(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] }), options, clock);
            Payslips = new PayslipService(Payroll, Employees, users, Audit, User, options, clock);
            Reports = new PayrollReportService(new FakePayrollReportRepository(Payroll, Employees), Audit, User);
        }

        private Employee Add(string code, string name, decimal basic, EmployeeSalaryStructure? structure)
        {
            var department = new Department { Name = "Engineering" };
            var employee = new Employee
            {
                EmployeeCode = code, FirstName = name, LastName = "Test", Status = EmployeeStatus.Active, JoiningDate = new DateTime(2021, 3, 15),
                BasicSalary = basic, Department = department, DepartmentId = department.Id, Designation = new Designation { Name = "Engineer" },
            };
            if (structure is not null)
            {
                structure.EmployeeId = employee.Id;
                employee.SalaryStructure = structure;
            }

            Employees.Employees.Add(employee);
            return employee;
        }

        public async Task<Guid> PeriodAsync(int month, PayrollPeriodStatus until)
        {
            User.SignInAs(UserRole.HR);
            var start = new DateOnly(2026, month, 1);
            var period = await Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = start, EndDate = start.AddMonths(1).AddDays(-1) }, CancellationToken.None);
            await Payrolls.CalculateAsync(period.Id, CancellationToken.None);
            if (until == PayrollPeriodStatus.Calculated)
            {
                return period.Id;
            }

            await Payrolls.SubmitAsync(period.Id, CancellationToken.None);
            User.SignInAs(UserRole.Admin);
            await Payrolls.ApproveAsync(period.Id, CancellationToken.None);
            if (until == PayrollPeriodStatus.Finalized)
            {
                await Payrolls.FinalizeAsync(period.Id, CancellationToken.None);
            }

            return period.Id;
        }

        public Payslip PayslipOf(Employee employee, Guid periodId) => Payroll.Payslips.Single(p => p.EmployeeId == employee.Id && p.PayrollPeriodId == periodId);
    }

    // ---- finalized-only rule ----

    [Theory]
    [InlineData(PayrollPeriodStatus.Calculated)]
    [InlineData(PayrollPeriodStatus.Approved)]
    public async Task NoPayslip_BeforeFinalization(PayrollPeriodStatus until)
    {
        var s = new Setup();
        await s.PeriodAsync(10, until);

        Assert.Empty(s.Payroll.Payslips);
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        Assert.Empty((await s.Payslips.GetMyPayslipsAsync(new PayrollHistoryQueryDto(), CancellationToken.None)).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetMyCurrentPayslipAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Finalization_IssuesPayslips_AndAuditsIt()
    {
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);

        Assert.Equal(2, s.Payroll.Payslips.Count(p => p.PayrollPeriodId == id));
        Assert.Contains(s.Audit.Entries, e => e.Action == AuditActions.PayslipsGenerated && e.Details == "2 payslips issued at finalization.");
    }

    [Fact]
    public async Task LegacyPayslipOfNonFinalPayroll_IsHiddenFromTheEmployee_ButNotFromHr()
    {
        // A payslip issued at approval before Day 20 must not leak: employees only see finalized or paid payroll.
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        s.Payroll.Periods.Single(p => p.Id == id).Status = PayrollPeriodStatus.Approved;
        var payslip = s.PayslipOf(s.Alice, id);

        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetByIdAsync(payslip.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetPdfAsync(payslip.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.RecordPrintAsync(payslip.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetByRecordAsync(payslip.PayrollRecordId, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetMyCurrentPayslipAsync(CancellationToken.None));
        Assert.Empty((await s.Payslips.GetMyPayslipsAsync(new PayrollHistoryQueryDto(), CancellationToken.None)).Items);
        Assert.Empty((await s.Payslips.GetEmployeePayslipsAsync(s.Alice.Id, new PayrollHistoryQueryDto(), CancellationToken.None)).Items);
        Assert.Empty((await s.Payrolls.GetEmployeeHistoryAsync(s.Alice.Id, CancellationToken.None)));
        Assert.Equal(0, (await s.Reports.GetMySummaryAsync(2026, 2026, CancellationToken.None)).PayslipCount);

        s.User.SignInAs(UserRole.HR);
        Assert.Equal("EMP-001", (await s.Payslips.GetByIdAsync(payslip.Id, CancellationToken.None)).EmployeeCode);
    }

    [Fact]
    public async Task PayslipNumber_IsStable_AndNeverDuplicated()
    {
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        var payslip = s.PayslipOf(s.Alice, id);

        var first = await s.Payslips.GetByIdAsync(payslip.Id, CancellationToken.None);
        var second = await s.Payslips.GetByIdAsync(payslip.Id, CancellationToken.None);
        var generated = await s.Payslips.GenerateAsync(id, CancellationToken.None);

        Assert.Equal("PS-20261001-EMP-001", first.PayslipNumber);
        Assert.Equal(first.PayslipNumber, second.PayslipNumber);
        Assert.Equal(0, generated.Generated);
        Assert.Equal(s.Payroll.Payslips.Count(), s.Payroll.Payslips.Select(p => p.PayslipNumber).Distinct().Count());
    }

    [Fact]
    public async Task Payslip_ShowsStandardLines_EvenWhenZero_AndTheJoiningDate()
    {
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        s.User.SignInAs(UserRole.Employee, s.Bob.Id);

        var payslip = await s.Payslips.GetByIdAsync(s.PayslipOf(s.Bob, id).Id, CancellationToken.None);

        Assert.Equal(new[] { "Basic salary", "House rent", "Medical allowance", "Transport allowance", "Other allowance" }, payslip.Earnings.Select(l => l.Label));
        Assert.Equal(new[] { "Tax", "Provident fund", "Unpaid leave", "Loan", "Other deduction" }, payslip.Deductions.Select(l => l.Label));
        Assert.All(payslip.Deductions, l => Assert.Equal(0m, l.Amount));
        Assert.Equal(0m, payslip.TotalDeduction);
        Assert.Equal(new DateOnly(2021, 3, 15), payslip.JoiningDate);
    }

    // ---- PDF and print ----

    [Fact]
    public async Task Pdf_IsAValidDocument_WithThePayslipValues_AndIsAudited()
    {
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        var payslip = s.PayslipOf(s.Alice, id);
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        s.Audit.Entries.Clear();

        var file = await s.Payslips.GetPdfAsync(payslip.Id, CancellationToken.None);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("PS-20261001-EMP-001.pdf", file.FileName);
        var text = Encoding.Latin1.GetString(file.Content);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        foreach (var part in new[] { "PS-20261001-EMP-001", "Alice \\(Ops\\) Test", "EMP-001", "Engineering", "15 Mar 2021", "October 2026", "28,400.00", "31,500.00", "3,100.00", "Provident fund", "NET SALARY", "Test Co" })
        {
            Assert.Contains(part, text);
        }

        AssertXrefIsCorrect(text);
        var entry = Assert.Single(s.Audit.Entries);
        Assert.Equal(AuditActions.PayslipDownloaded, entry.Action);
        Assert.DoesNotContain("28400", entry.Details);
    }

    [Fact]
    public async Task Pdf_And_Print_FollowTheViewAccessRule()
    {
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        var bobs = s.PayslipOf(s.Bob, id);
        s.Bob.ManagerId = s.Alice.Id;

        foreach (var role in new[] { UserRole.Employee, UserRole.Manager })
        {
            s.User.SignInAs(role, s.Alice.Id);
            await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetPdfAsync(bobs.Id, CancellationToken.None));
            await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.RecordPrintAsync(bobs.Id, CancellationToken.None));
        }

        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetPdfAsync(Guid.NewGuid(), CancellationToken.None));
        foreach (var role in new[] { UserRole.HR, UserRole.Admin })
        {
            s.User.SignInAs(role);
            Assert.NotEmpty((await s.Payslips.GetPdfAsync(bobs.Id, CancellationToken.None)).Content);
        }
    }

    [Fact]
    public async Task Print_IsAudited()
    {
        var s = new Setup();
        var id = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        s.Audit.Entries.Clear();

        await s.Payslips.RecordPrintAsync(s.PayslipOf(s.Alice, id).Id, CancellationToken.None);

        var entry = Assert.Single(s.Audit.Entries);
        Assert.Equal(AuditActions.PayslipPrinted, entry.Action);
        Assert.Equal("Payslip PS-20261001-EMP-001 of EMP-001, October 2026.", entry.Details);
    }

    [Fact]
    public void SimplePdf_EscapesText_AndReplacesUnsupportedCharacters()
    {
        var bytes = new SimplePdf("t").Text(10, 10, "a(b)c\\ – ক", 10).ToBytes();
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("(a\\(b\\)c\\\\ \u0096 ?) Tj", text);
        AssertXrefIsCorrect(text);
    }

    private static void AssertXrefIsCorrect(string pdf)
    {
        var xref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)").Groups[1].Value);
        Assert.StartsWith("xref", pdf[xref..]);
        var offsets = Regex.Matches(pdf[xref..], @"(\d{10}) 00000 n").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        Assert.Equal(7, offsets.Count);
        for (var i = 0; i < offsets.Count; i++)
        {
            Assert.StartsWith($"{i + 1} 0 obj", pdf[offsets[i]..]);
        }
    }

    // ---- own summary ----

    [Fact]
    public async Task MySummary_CountsOnlyOwnFinalizedPayroll()
    {
        var s = new Setup();
        var october = await s.PeriodAsync(10, PayrollPeriodStatus.Finalized);
        await s.PeriodAsync(9, PayrollPeriodStatus.Finalized);
        await s.PeriodAsync(11, PayrollPeriodStatus.Approved); // not final: excluded
        s.PayslipOf(s.Alice, october).PaymentStatus = PaymentStatus.Paid;
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var summary = await s.Reports.GetMySummaryAsync(null, 2026, CancellationToken.None);

        Assert.Equal(2026, summary.Year);
        Assert.Equal(2, summary.PayslipCount);
        Assert.Equal(56800m, summary.NetSalary);
        Assert.Equal(63000m, summary.GrossSalary);
        Assert.Equal(28400m, summary.PaidNetSalary);
        Assert.Equal("October 2026", summary.Latest!.PeriodName);
        Assert.Equal("Paid", summary.Latest.PaymentStatus);
        Assert.Equal(0, (await s.Reports.GetMySummaryAsync(2025, 2026, CancellationToken.None)).PayslipCount);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Reports.GetMySummaryAsync(1999, 2026, CancellationToken.None));
    }

    [Fact]
    public async Task MySummary_WithoutPayroll_IsEmpty()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Bob.Id);

        var summary = await s.Reports.GetMySummaryAsync(null, 2026, CancellationToken.None);

        Assert.Equal(0, summary.PayslipCount);
        Assert.Null(summary.Latest);
    }
}
