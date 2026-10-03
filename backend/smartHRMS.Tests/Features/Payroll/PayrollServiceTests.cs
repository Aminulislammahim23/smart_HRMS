using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Payroll;

public class PayrollServiceTests
{
    // October 2026: 21 working days (Friday/Saturday weekend).
    private static readonly DateOnly OctStart = new(2026, 10, 1);
    private static readonly DateOnly OctEnd = new(2026, 10, 31);

    private sealed class Setup
    {
        public FakeEmployeeRepository Employees { get; } = new();
        public FakeAttendanceRepository Attendance { get; }
        public FakeLeaveRequestRepository Leave { get; }
        public FakePayrollRepository Payroll { get; }
        public FakeUserRepository Users { get; } = new();
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = FakeCurrentUser.As(UserRole.HR);
        public PayrollOptions Options { get; } = new() { CompanyName = "Test Co", CompanyAddress = "Dhaka" };
        public PayrollService Service { get; }

        public Employee Alice { get; }
        public Employee Bob { get; }
        public Employee NoSalary { get; }
        public Employee Resigned { get; }

        public Setup()
        {
            Attendance = new FakeAttendanceRepository(Employees);
            Leave = new FakeLeaveRequestRepository(Employees);
            Payroll = new FakePayrollRepository(Employees);

            Alice = AddEmployee("EMP-001", "Alice", 21000, new EmployeeSalaryStructure { HouseRent = 10500, MedicalAllowance = 1500, MonthlyTax = 1000 });
            Bob = AddEmployee("EMP-002", "Bob", 42000, null);
            NoSalary = AddEmployee("EMP-003", "Nosal", null, null);
            Resigned = AddEmployee("EMP-004", "Gone", 30000, null, EmployeeStatus.Resigned);

            Service = new PayrollService(Payroll, Employees, Attendance, Leave, Users, Audit, User,
                new WorkCalendar(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] }), Options);
        }

        public Employee AddEmployee(string code, string name, decimal? basic, EmployeeSalaryStructure? structure, EmployeeStatus status = EmployeeStatus.Active, DateTime? joined = null)
        {
            var employee = new Employee
            {
                EmployeeCode = code,
                FirstName = name,
                LastName = "Test",
                Status = status,
                JoiningDate = joined ?? new DateTime(2020, 1, 1),
                BasicSalary = basic,
                Department = new Department { Name = "Engineering" },
                Designation = new Designation { Name = "Engineer" },
            };
            if (structure is not null)
            {
                structure.EmployeeId = employee.Id;
                employee.SalaryStructure = structure;
            }

            Employees.Employees.Add(employee);
            return employee;
        }

        public async Task<PayrollPeriodDto> CreateOctoberAsync() =>
            await Service.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = OctStart, EndDate = OctEnd }, CancellationToken.None);

        public async Task<Guid> CalculatedOctoberAsync()
        {
            var period = await CreateOctoberAsync();
            await Service.CalculateAsync(period.Id, CancellationToken.None);
            return period.Id;
        }

        public async Task<Guid> ApprovedOctoberAsync()
        {
            var id = await CalculatedOctoberAsync();
            await Service.SubmitAsync(id, CancellationToken.None);
            User.SignInAs(UserRole.Admin);
            await Service.ApproveAsync(id, CancellationToken.None);
            return id;
        }

        public PayrollRecord RecordOf(Employee employee) => Payroll.Records.Single(r => r.EmployeeId == employee.Id);
    }

    // ---- periods ----

    [Fact]
    public async Task CreatePeriod_FullMonth_IsDraftWithMonthName()
    {
        var s = new Setup();

        var period = await s.CreateOctoberAsync();

        Assert.Equal("Draft", period.Status);
        Assert.Equal("October 2026", period.Name);
        Assert.Equal(21, period.WorkingDays);
        Assert.Contains(s.Audit.Entries, e => e.Action == "PayrollPeriodCreated");
    }

    [Theory]
    [InlineData("2026-10-31", "2026-10-01")]
    [InlineData("2026-10-01", "2026-10-01")]
    public async Task CreatePeriod_StartNotBeforeEnd_IsRejected(string start, string end)
    {
        var s = new Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreatePeriodAsync(
            new CreatePayrollPeriodDto { StartDate = DateOnly.Parse(start), EndDate = DateOnly.Parse(end) }, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePeriod_LongerThan31Days_IsRejected()
    {
        var s = new Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreatePeriodAsync(
            new CreatePayrollPeriodDto { StartDate = OctStart, EndDate = new DateOnly(2026, 11, 15) }, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePeriod_Duplicate_IsRejected()
    {
        var s = new Setup();
        await s.CreateOctoberAsync();

        await Assert.ThrowsAsync<ConflictException>(() => s.CreateOctoberAsync());
    }

    [Fact]
    public async Task CreatePeriod_Overlapping_IsRejected()
    {
        var s = new Setup();
        await s.CreateOctoberAsync();

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CreatePeriodAsync(
            new CreatePayrollPeriodDto { StartDate = new DateOnly(2026, 10, 20), EndDate = new DateOnly(2026, 11, 19) }, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePeriod_SameDatesAsCancelledPeriod_IsAllowed()
    {
        var s = new Setup();
        var first = await s.CreateOctoberAsync();
        await s.Service.CancelAsync(first.Id, new CancelPayrollDto { Reason = "Wrong" }, CancellationToken.None);

        var second = await s.CreateOctoberAsync();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Manager)]
    public async Task NonHrUsers_CannotManagePayroll(UserRole role)
    {
        var s = new Setup();
        var period = await s.CreateOctoberAsync();
        s.User.SignInAs(role, s.Alice.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.CreateOctoberAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetPeriodsAsync(new PayrollPeriodQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CalculateAsync(period.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetRecordsAsync(period.Id, new PayrollRecordQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.SubmitAsync(period.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ApproveAsync(period.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkPaidAsync(period.Id, CancellationToken.None));
    }

    // ---- calculation ----

    [Fact]
    public async Task Calculate_CreatesOneRecordPerEligibleEmployee_AndReportsSkipped()
    {
        var s = new Setup();
        var period = await s.CreateOctoberAsync();

        var result = await s.Service.CalculateAsync(period.Id, CancellationToken.None);

        Assert.Equal(2, result.Created);
        Assert.Equal("Calculated", result.Period.Status);
        Assert.Equal(2, result.Period.EmployeeCount);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("EMP-003", skipped.EmployeeCode);
        Assert.DoesNotContain(s.Payroll.Records, r => r.EmployeeId == s.Resigned.Id);
        Assert.Equal(1, s.Payroll.SaveCount - 1); // one save for create, one for the whole calculation
    }

    [Fact]
    public async Task Calculate_UsesSalaryStructure_AndFormulas()
    {
        var s = new Setup();
        await s.CalculatedOctoberAsync();

        var alice = s.RecordOf(s.Alice);
        Assert.Equal(21000m, alice.BasicSalary);
        Assert.Equal(10500m, alice.HouseRent);
        Assert.Equal(1500m, alice.MedicalAllowance);
        Assert.Equal(33000m, alice.GrossSalary);
        Assert.Equal(1000m, alice.TotalDeduction);
        Assert.Equal(32000m, alice.NetSalary);
        Assert.Equal("EMP-001", alice.EmployeeCode);
        Assert.Equal("Engineering", alice.DepartmentName);
    }

    [Fact]
    public async Task Calculate_OnlyApprovedUnpaidLeaveIsDeducted()
    {
        var s = new Setup();
        void AddLeave(LeaveType type, LeaveStatus status, int fromDay, int toDay) => s.Leave.Requests.Add(new LeaveRequest
        {
            EmployeeId = s.Alice.Id, LeaveType = type, Status = status,
            StartDate = new DateOnly(2026, 10, fromDay), EndDate = new DateOnly(2026, 10, toDay),
        });

        AddLeave(LeaveType.Unpaid, LeaveStatus.Approved, 5, 6);   // Mon-Tue: 2 working days
        AddLeave(LeaveType.Unpaid, LeaveStatus.Pending, 12, 12);
        AddLeave(LeaveType.Unpaid, LeaveStatus.Rejected, 13, 13);
        AddLeave(LeaveType.Annual, LeaveStatus.Approved, 14, 14);

        await s.CalculatedOctoberAsync();

        var alice = s.RecordOf(s.Alice);
        Assert.Equal(2m, alice.UnpaidLeaveDays);
        Assert.Equal(1m, alice.PaidLeaveDays);
        Assert.Equal(2000m, alice.LeaveDeduction); // 21000 / 21 * 2
        Assert.Equal(30000m, alice.NetSalary);     // 33000 - 1000 tax - 2000 leave
    }

    [Fact]
    public async Task Calculate_UsesAttendance_WithoutChangingIt()
    {
        var s = new Setup();
        var attendance = new Attendance { EmployeeId = s.Bob.Id, AttendanceDate = new DateOnly(2026, 10, 1), Status = AttendanceStatus.Present };
        s.Attendance.Records.Add(attendance);
        s.Attendance.Records.Add(new Attendance { EmployeeId = s.Bob.Id, AttendanceDate = new DateOnly(2026, 10, 4), Status = AttendanceStatus.Absent });

        await s.CalculatedOctoberAsync();

        var bob = s.RecordOf(s.Bob);
        Assert.Equal(1m, bob.PresentDays);
        Assert.Equal(1m, bob.AbsentDays);
        Assert.Equal(0m, bob.LeaveDeduction); // absences are not deducted by default
        Assert.Equal(AttendanceStatus.Present, attendance.Status);
        Assert.Equal(2, s.Attendance.Records.Count);
    }

    [Fact]
    public async Task Recalculate_KeepsManualAmounts_AndNeverDuplicates()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        var alice = s.RecordOf(s.Alice);
        await s.Service.UpdateRecordAsync(alice.Id, new UpdatePayrollRecordDto { Bonus = 5000, OvertimeAmount = 1200, LoanDeduction = 700 }, CancellationToken.None);

        s.Alice.BasicSalary = 42000;
        var result = await s.Service.CalculateAsync(id, CancellationToken.None);

        Assert.Equal(0, result.Created);
        Assert.Equal(2, result.Updated);
        Assert.Single(s.Payroll.Records, r => r.EmployeeId == s.Alice.Id);
        var updated = s.RecordOf(s.Alice);
        Assert.Equal(42000m, updated.BasicSalary);
        Assert.Equal(5000m, updated.Bonus);
        Assert.Equal(1200m, updated.OvertimeAmount);
        Assert.Equal(700m, updated.LoanDeduction);
    }

    [Fact]
    public async Task Recalculate_RemovesEmployeesWhoAreNoLongerEligible()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        s.Bob.Status = EmployeeStatus.Inactive;

        var result = await s.Service.CalculateAsync(id, CancellationToken.None);

        Assert.Equal(1, result.Removed);
        Assert.DoesNotContain(s.Payroll.Records, r => r.EmployeeId == s.Bob.Id);
    }

    [Fact]
    public async Task Calculate_JoinerAfterPeriod_IsNotIncluded_JoinerDuringPeriod_IsProrated()
    {
        var s = new Setup();
        s.AddEmployee("EMP-005", "Later", 21000, null, joined: new DateTime(2026, 11, 2));
        var mid = s.AddEmployee("EMP-006", "Mid", 21000, null, joined: new DateTime(2026, 10, 25));

        await s.CalculatedOctoberAsync();

        Assert.DoesNotContain(s.Payroll.Records, r => r.EmployeeCode == "EMP-005");
        var record = s.RecordOf(mid);
        Assert.Equal(5m, record.WorkingDays);
        Assert.Equal(5000m, record.BasicSalary); // 21000 * 5/21
    }

    [Fact]
    public async Task Calculate_WithNoEligibleEmployees_GivesEmptyPayroll_ThatCannotBeSubmitted()
    {
        var s = new Setup();
        s.Employees.Employees.Clear();

        var id = await s.CalculatedOctoberAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.SubmitAsync(id, CancellationToken.None));
    }

    // ---- editing and workflow ----

    [Fact]
    public async Task UpdateRecord_NegativeNet_NeedsReview_AndBlocksSubmission()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();

        var dto = await s.Service.UpdateRecordAsync(s.RecordOf(s.Alice).Id, new UpdatePayrollRecordDto { LoanDeduction = 50000 }, CancellationToken.None);

        Assert.Equal("NeedsReview", dto.Status);
        Assert.True(dto.NetSalary < 0);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.SubmitAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Workflow_DraftToPaid_RecordsWhoAndWhen()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();

        var submitted = await s.Service.SubmitAsync(id, CancellationToken.None);
        Assert.Equal("PendingApproval", submitted.Status);

        s.User.SignInAs(UserRole.Admin);
        var approved = await s.Service.ApproveAsync(id, CancellationToken.None);
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.ApprovedAt);
        Assert.All(s.Payroll.Records, r => Assert.Equal(PayrollRecordStatus.Approved, r.Status));

        var paid = await s.Service.MarkPaidAsync(id, CancellationToken.None);
        Assert.Equal("Paid", paid.Status);
        Assert.NotNull(paid.PaidAt);
        Assert.All(s.Payroll.Records, r => Assert.Equal(PayrollRecordStatus.Paid, r.Status));
        Assert.Contains(s.Audit.Entries, e => e.Action == "PayrollApproved");
        Assert.Contains(s.Audit.Entries, e => e.Action == "PayrollPaid");
    }

    [Fact]
    public async Task HrCannotApproveOrMarkPaid()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        await s.Service.SubmitAsync(id, CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ApproveAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task CannotMarkPaid_BeforeApproval()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        s.User.SignInAs(UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(id, CancellationToken.None));
        await s.Service.SubmitAsync(id, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task CannotApprove_BeforeSubmission()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        s.User.SignInAs(UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.ApproveAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task AdminCannotApprove_PayrollContainingOwnSalary()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        await s.Service.SubmitAsync(id, CancellationToken.None);
        s.User.SignInAs(UserRole.Admin, s.Alice.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ApproveAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task ApprovedPayroll_CannotBeEdited_Recalculated_Cancelled_OrDeleted()
    {
        var s = new Setup();
        var id = await s.ApprovedOctoberAsync();
        var record = s.RecordOf(s.Alice);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.UpdateRecordAsync(record.Id, new UpdatePayrollRecordDto { Bonus = 1 }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CalculateAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CancelAsync(id, new CancelPayrollDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.DeletePeriodAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.UpdatePeriodAsync(id, new UpdatePayrollPeriodDto { StartDate = OctStart, EndDate = OctEnd }, CancellationToken.None));
        Assert.Equal(0m, record.Bonus);
    }

    [Fact]
    public async Task PaidPayroll_CannotBeEdited()
    {
        var s = new Setup();
        var id = await s.ApprovedOctoberAsync();
        await s.Service.MarkPaidAsync(id, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.UpdateRecordAsync(s.RecordOf(s.Alice).Id, new UpdatePayrollRecordDto { Bonus = 1 }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CalculateAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task DraftPeriod_CanBeEditedAndDeleted_CalculatedCannotBeDeleted()
    {
        var s = new Setup();
        var period = await s.CreateOctoberAsync();

        var updated = await s.Service.UpdatePeriodAsync(period.Id, new UpdatePayrollPeriodDto { Name = "Oct run", StartDate = OctStart, EndDate = OctEnd }, CancellationToken.None);
        Assert.Equal("Oct run", updated.Name);

        await s.Service.CalculateAsync(period.Id, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.DeletePeriodAsync(period.Id, CancellationToken.None));
    }

    // ---- employee access ----

    [Fact]
    public async Task Employee_SeesOwnRecordOnlyAfterApproval()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();
        var aliceRecord = s.RecordOf(s.Alice);

        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetRecordAsync(aliceRecord.Id, CancellationToken.None));
        Assert.Empty(await s.Service.GetEmployeeHistoryAsync(s.Alice.Id, CancellationToken.None));

        s.User.SignInAs(UserRole.HR);
        await s.Service.SubmitAsync(id, CancellationToken.None);
        s.User.SignInAs(UserRole.Admin);
        await s.Service.ApproveAsync(id, CancellationToken.None);

        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        var record = await s.Service.GetRecordAsync(aliceRecord.Id, CancellationToken.None);
        Assert.Equal(32000m, record.NetSalary);
        Assert.Single(await s.Service.GetEmployeeHistoryAsync(s.Alice.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Employee_CannotSeeAnotherEmployeesPayroll()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        var bobRecord = s.RecordOf(s.Bob);

        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetRecordAsync(bobRecord.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetPayslipAsync(bobRecord.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetEmployeeHistoryAsync(s.Bob.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Manager_HasNoPayrollAccessToReports()
    {
        var s = new Setup();
        s.Bob.ManagerId = s.Alice.Id;
        await s.ApprovedOctoberAsync();

        s.User.SignInAs(UserRole.Manager, s.Alice.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetEmployeeHistoryAsync(s.Bob.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetPayslipAsync(s.RecordOf(s.Bob).Id, CancellationToken.None));
    }

    [Fact]
    public async Task Payslip_ShowsCorrectAmounts_AndIsAudited()
    {
        var s = new Setup();
        await s.ApprovedOctoberAsync();
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var payslip = await s.Service.GetPayslipAsync(s.RecordOf(s.Alice).Id, CancellationToken.None);

        Assert.Equal("Test Co", payslip.CompanyName);
        Assert.Equal("EMP-001", payslip.EmployeeCode);
        Assert.Equal("Engineering", payslip.DepartmentName);
        Assert.Equal("October 2026", payslip.PeriodName);
        Assert.Equal(33000m, payslip.GrossSalary);
        Assert.Equal(1000m, payslip.TotalDeduction);
        Assert.Equal(32000m, payslip.NetSalary);
        Assert.Equal(payslip.GrossSalary, payslip.Earnings.Sum(l => l.Amount));
        Assert.Equal(payslip.TotalDeduction, payslip.Deductions.Sum(l => l.Amount));
        Assert.Contains(payslip.Earnings, l => l.Label == "House rent" && l.Amount == 10500m);
        Assert.Equal("Approved", payslip.PaymentStatus);
        Assert.True(payslip.IsFinal);
        Assert.Contains(s.Audit.Entries, e => e.Action == "PayslipViewed");
    }

    [Fact]
    public async Task Records_FilterBySearchAndStatus()
    {
        var s = new Setup();
        var id = await s.CalculatedOctoberAsync();

        var bySearch = await s.Service.GetRecordsAsync(id, new PayrollRecordQueryDto { Search = "bob" }, CancellationToken.None);
        Assert.Equal("EMP-002", Assert.Single(bySearch).EmployeeCode);

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.GetRecordsAsync(id, new PayrollRecordQueryDto { Status = "Bogus" }, CancellationToken.None));
    }
}
