using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Payroll;

public class PayslipServiceTests
{
    private static readonly DateOnly OctStart = new(2026, 10, 1);
    private static readonly DateOnly OctEnd = new(2026, 10, 31);

    private sealed class Setup
    {
        public FakeEmployeeRepository Employees { get; } = new();
        public FakePayrollRepository Payroll { get; }
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = FakeCurrentUser.As(UserRole.HR);
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 5, 6, 0, 0, TimeSpan.Zero)); // office date 2026-11-05
        public PayrollService Payrolls { get; }
        public PayslipService Payslips { get; }
        public Employee Alice { get; }
        public Employee Bob { get; }

        public Setup()
        {
            Payroll = new FakePayrollRepository(Employees);
            var department = new Department { Name = "Engineering" };
            Alice = Add("EMP-001", "Alice", 21000, new EmployeeSalaryStructure { HouseRent = 10500, MonthlyTax = 1000, MonthlyProvidentFund = 2100 }, department);
            Bob = Add("EMP-002", "Bob", 42000, null, new Department { Name = "Sales" });

            var users = new FakeUserRepository();
            var clock = new AttendanceClock(Clock, new AttendanceOptions());
            var options = new PayrollOptions { CompanyName = "Test Co" };
            Payrolls = new PayrollService(Payroll, Employees, new FakeAttendanceRepository(Employees), new FakeLeaveRequestRepository(Employees), users, Audit, User,
                new WorkCalendar(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] }), options, clock);
            Payslips = new PayslipService(Payroll, Employees, users, Audit, User, options, clock);
        }

        private Employee Add(string code, string name, decimal basic, EmployeeSalaryStructure? structure, Department department)
        {
            var employee = new Employee
            {
                EmployeeCode = code, FirstName = name, LastName = "Test", Status = EmployeeStatus.Active, JoiningDate = new DateTime(2020, 1, 1),
                BasicSalary = basic, Department = department, DepartmentId = department.Id, Designation = new Designation { Name = "Engineer" },
                PhotoUrl = $"/uploads/employees/{code}.jpg",
            };
            if (structure is not null)
            {
                structure.EmployeeId = employee.Id;
                employee.SalaryStructure = structure;
            }

            Employees.Employees.Add(employee);
            return employee;
        }

        /// <summary>Creates, calculates and submits October 2026 as HR; returns the period id (PendingApproval).</summary>
        public async Task<Guid> SubmittedOctoberAsync()
        {
            User.SignInAs(UserRole.HR);
            var period = await Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = OctStart, EndDate = OctEnd }, CancellationToken.None);
            await Payrolls.CalculateAsync(period.Id, CancellationToken.None);
            await Payrolls.SubmitAsync(period.Id, CancellationToken.None);
            return period.Id;
        }

        public async Task<Guid> ApprovedOctoberAsync()
        {
            var id = await SubmittedOctoberAsync();
            User.SignInAs(UserRole.Admin);
            await Payrolls.ApproveAsync(id, CancellationToken.None);
            return id;
        }

        public Payslip PayslipOf(Employee employee) => Payroll.Payslips.Single(p => p.EmployeeId == employee.Id);
    }

    // ---- issuing ----

    [Fact]
    public async Task NoPayslips_BeforeApproval_AndGenerationIsRefused()
    {
        var s = new Setup();
        var id = await s.SubmittedOctoberAsync();

        Assert.Empty(s.Payroll.Payslips);
        await Assert.ThrowsAsync<ConflictException>(() => s.Payslips.GenerateAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Approval_IssuesOneUnpaidPayslipPerRecord_InTheSameSave()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();

        Assert.Equal(2, s.Payroll.Payslips.Count());
        var payslip = s.PayslipOf(s.Alice);
        Assert.Equal("PS-20261001-EMP-001", payslip.PayslipNumber);
        Assert.Equal(PaymentStatus.Unpaid, payslip.PaymentStatus);
        Assert.Null(payslip.PaymentDate);
        Assert.Contains(s.Audit.Entries, e => e.Action == "PayslipsGenerated");
    }

    [Fact]
    public async Task Generate_IsIdempotent_ForApprovedPayroll()
    {
        var s = new Setup();
        var id = await s.ApprovedOctoberAsync();

        var result = await s.Payslips.GenerateAsync(id, CancellationToken.None);

        Assert.Equal(0, result.Generated);
        Assert.Equal(2, result.AlreadyGenerated);
        Assert.Equal(2, s.Payroll.Payslips.Count());
    }

    [Fact]
    public async Task Payslip_UsesTheLockedRecordAmounts_IncludingProvidentFund()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var payslip = await s.Payslips.GetByIdAsync(s.PayslipOf(s.Alice).Id, CancellationToken.None);

        // Gross 21000 + 10500; deductions tax 1000 + PF 2100.
        Assert.Equal(31500m, payslip.GrossSalary);
        Assert.Equal(3100m, payslip.TotalDeduction);
        Assert.Equal(28400m, payslip.NetSalary);
        Assert.Contains(payslip.Deductions, l => l.Label == "Provident fund" && l.Amount == 2100m);
        Assert.Equal(payslip.TotalDeduction, payslip.Deductions.Sum(l => l.Amount));
        Assert.Equal("Approved", payslip.PayrollStatus);
        Assert.Equal("Unpaid", payslip.PaymentStatus);
        Assert.True(payslip.IsFinal);
        Assert.Equal("/uploads/employees/EMP-001.jpg", payslip.EmployeePhotoUrl);
        Assert.Equal("PS-20261001-EMP-001", payslip.PayslipNumber);
    }

    // ---- employee isolation ----

    [Fact]
    public async Task Employee_SeesOwnPayslip_NotAnotherEmployees()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        Assert.Equal("EMP-001", (await s.Payslips.GetByIdAsync(s.PayslipOf(s.Alice).Id, CancellationToken.None)).EmployeeCode);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetByIdAsync(s.PayslipOf(s.Bob).Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payslips.GetEmployeePayslipsAsync(s.Bob.Id, new PayrollHistoryQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payslips.GetPayslipsAsync(new PayrollHistoryQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payslips.GenerateAsync(s.Payroll.Periods.Single().Id, CancellationToken.None));
    }

    [Fact]
    public async Task MyEndpoints_UseTheTokenEmployee_IgnoringAnyEmployeeIdSent()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var mine = await s.Payslips.GetMyPayslipsAsync(new PayrollHistoryQueryDto { EmployeeId = s.Bob.Id }, CancellationToken.None);

        Assert.Equal(s.Alice.Id, Assert.Single(mine.Items).EmployeeId);
        Assert.Equal("EMP-001", (await s.Payslips.GetMyCurrentPayslipAsync(CancellationToken.None)).EmployeeCode);
    }

    [Fact]
    public async Task MyPayments_ListOnlyPaidPayslips()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        Assert.Empty((await s.Payslips.GetMyPaymentsAsync(new PayrollHistoryQueryDto(), CancellationToken.None)).Items);

        // Payment itself goes through payment batches (PaymentServiceTests); here only the listing is tested.
        var paid = s.PayslipOf(s.Alice);
        paid.PaymentStatus = PaymentStatus.Paid;
        paid.PaymentDate = new DateOnly(2026, 11, 2);

        var payments = await s.Payslips.GetMyPaymentsAsync(new PayrollHistoryQueryDto(), CancellationToken.None);
        Assert.Equal("Paid", Assert.Single(payments.Items).PaymentStatus);
    }

    [Fact]
    public async Task Employee_WithNoPayslip_GetsNotFoundForCurrent_AndNoPreviewByRecord()
    {
        var s = new Setup();
        await s.SubmittedOctoberAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetMyCurrentPayslipAsync(CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetByRecordAsync(s.Payroll.Records.First(r => r.EmployeeId == s.Alice.Id).Id, CancellationToken.None));

        s.User.SignInAs(UserRole.HR);
        var preview = await s.Payslips.GetByRecordAsync(s.Payroll.Records.First(r => r.EmployeeId == s.Alice.Id).Id, CancellationToken.None);
        Assert.False(preview.IsFinal);
        Assert.Null(preview.PayslipId);
    }

    [Fact]
    public async Task Manager_HasNoAccessToReportsPayslips()
    {
        var s = new Setup();
        s.Bob.ManagerId = s.Alice.Id;
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.Manager, s.Alice.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => s.Payslips.GetByIdAsync(s.PayslipOf(s.Bob).Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payslips.GetEmployeePayslipsAsync(s.Bob.Id, new PayrollHistoryQueryDto(), CancellationToken.None));
    }

    // ---- history ----

    [Fact]
    public async Task History_FiltersAndPages()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.HR);

        var all = await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { PageSize = 1 }, CancellationToken.None);
        Assert.Equal(2, all.TotalCount);
        Assert.Single(all.Items);
        Assert.Equal(2, all.TotalPages);

        Assert.Equal(2, (await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { Year = 2026, Month = 10, Status = "Approved" }, CancellationToken.None)).TotalCount);
        Assert.Equal(0, (await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { Month = 9 }, CancellationToken.None)).TotalCount);
        Assert.Equal(0, (await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { PaymentStatus = "Paid" }, CancellationToken.None)).TotalCount);
        Assert.Equal("EMP-002", Assert.Single((await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { Search = "bob" }, CancellationToken.None)).Items).EmployeeCode);
        Assert.Equal("EMP-001", Assert.Single((await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { DepartmentId = s.Alice.DepartmentId }, CancellationToken.None)).Items).EmployeeCode);
        var byNet = await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto { SortBy = "net", SortDirection = "desc" }, CancellationToken.None);
        Assert.Equal("EMP-002", byNet.Items[0].EmployeeCode);
    }

    [Theory]
    [InlineData(13, null, null, null, null, null)]
    [InlineData(null, 1999, null, null, null, null)]
    [InlineData(null, null, 0, null, null, null)]
    [InlineData(null, null, null, 101, null, null)]
    [InlineData(null, null, null, null, "salary", null)]
    [InlineData(null, null, null, null, null, "Bogus")]
    public async Task History_InvalidFilters_AreRejected(int? month, int? year, int? page, int? pageSize, string? sortBy, string? paymentStatus)
    {
        var s = new Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto
        {
            Month = month, Year = year, Page = page, PageSize = pageSize, SortBy = sortBy, PaymentStatus = paymentStatus,
        }, CancellationToken.None));
    }

    [Fact]
    public async Task History_ShowsWorkInProgress_PayslipListOnlyIssued()
    {
        var s = new Setup();
        await s.SubmittedOctoberAsync();

        Assert.Equal(2, (await s.Payslips.GetHistoryAsync(new PayrollHistoryQueryDto(), CancellationToken.None)).TotalCount);
        Assert.Equal(0, (await s.Payslips.GetPayslipsAsync(new PayrollHistoryQueryDto(), CancellationToken.None)).TotalCount);
    }

    [Fact]
    public void Calculator_ProvidentFund_IsProratedAndDeducted()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(21000, 0, 0, 0, 0, 0, 2100), new AttendanceSummary(21, 7, 7, 0, 0, 0), false);
        Assert.Equal(700m, amounts.ProvidentFund);

        var record = new PayrollRecord { BasicSalary = 7000, ProvidentFund = 700, Tax = 300 };
        PayrollCalculator.ApplyTotals(record);
        Assert.Equal(1000m, record.TotalDeduction);
        Assert.Equal(6000m, record.NetSalary);
    }
}
