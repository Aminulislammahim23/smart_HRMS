using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Leaves;
using smartHRMS.Application.Features.Leaves.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Leaves;

public class LeaveServiceTests
{
    private sealed class Setup
    {
        public FakeEmployeeRepository Employees { get; } = new();
        public FakeLeaveRequestRepository Leave { get; }
        public FakePayrollRepository Payroll { get; }
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = new();
        public LeaveService Service { get; }

        public Employee Manager { get; }
        public Employee Report { get; }
        public Employee Other { get; }

        public Setup()
        {
            Leave = new FakeLeaveRequestRepository(Employees);
            Payroll = new FakePayrollRepository(Employees);
            Manager = Add("EMP-010", null);
            Report = Add("EMP-011", Manager.Id);
            Other = Add("EMP-012", null);
            Service = new LeaveService(Leave, Employees, Payroll, new FakeUserRepository(), Audit, User,
                new WorkCalendar(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] }));
        }

        private Employee Add(string code, Guid? managerId)
        {
            var employee = new Employee { EmployeeCode = code, FirstName = code, LastName = "X", Status = EmployeeStatus.Active, JoiningDate = new DateTime(2020, 1, 1), ManagerId = managerId };
            Employees.Employees.Add(employee);
            return employee;
        }

        public Task<LeaveRequestDto> ApplyAsync(LeaveType type = LeaveType.Annual, int fromDay = 5, int toDay = 6, Guid? employeeId = null) =>
            Service.CreateAsync(new CreateLeaveRequestDto
            {
                EmployeeId = employeeId,
                LeaveType = type,
                StartDate = new DateOnly(2026, 10, fromDay),
                EndDate = new DateOnly(2026, 10, toDay),
            }, CancellationToken.None);
    }

    [Fact]
    public async Task Employee_AppliesForOwnLeave_CountingWorkingDaysOnly()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);

        // Thu 1 Oct .. Sun 4 Oct: Fri and Sat are weekend -> 2 working days.
        var request = await s.ApplyAsync(fromDay: 1, toDay: 4);

        Assert.Equal("Pending", request.Status);
        Assert.Equal(2, request.TotalDays);
        Assert.Equal(s.Report.Id, request.EmployeeId);
        Assert.Contains(s.Audit.Entries, e => e.Action == "LeaveRequested");
    }

    [Fact]
    public async Task Employee_CannotApplyForSomeoneElse_HrCan()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ApplyAsync(employeeId: s.Other.Id));

        s.User.SignInAs(UserRole.HR);
        var request = await s.ApplyAsync(employeeId: s.Other.Id);
        Assert.Equal(s.Other.Id, request.EmployeeId);
    }

    [Fact]
    public async Task WeekendOnlyRange_IsRejected()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);

        await Assert.ThrowsAsync<BadRequestException>(() => s.ApplyAsync(fromDay: 2, toDay: 3));
    }

    [Fact]
    public async Task OverlappingLeave_IsRejected()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        await s.ApplyAsync(fromDay: 5, toDay: 7);

        await Assert.ThrowsAsync<ConflictException>(() => s.ApplyAsync(fromDay: 7, toDay: 8));
    }

    [Fact]
    public async Task DirectManager_CanApprove_ButNotOtherEmployeesLeave()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        var reportLeave = await s.ApplyAsync();
        s.User.SignInAs(UserRole.Employee, s.Other.Id);
        var otherLeave = await s.ApplyAsync();

        s.User.SignInAs(UserRole.Manager, s.Manager.Id);
        var approved = await s.Service.ApproveAsync(reportLeave.Id, new ReviewLeaveRequestDto { Comment = "OK" }, CancellationToken.None);
        Assert.Equal("Approved", approved.Status);

        // Not their report: the request is invisible to them.
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.ApproveAsync(otherLeave.Id, new ReviewLeaveRequestDto(), CancellationToken.None));
    }

    [Fact]
    public async Task NobodyApprovesOwnLeave_NotEvenHr()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.HR, s.Other.Id);
        var own = await s.ApplyAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ApproveAsync(own.Id, new ReviewLeaveRequestDto(), CancellationToken.None));
    }

    [Fact]
    public async Task Employee_CannotApproveAnyone()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        var request = await s.ApplyAsync();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ApproveAsync(request.Id, new ReviewLeaveRequestDto(), CancellationToken.None));
    }

    [Fact]
    public async Task ReviewingTwice_IsAConflict()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        var request = await s.ApplyAsync();
        s.User.SignInAs(UserRole.HR);
        await s.Service.RejectAsync(request.Id, new ReviewLeaveRequestDto { Comment = "No" }, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.ApproveAsync(request.Id, new ReviewLeaveRequestDto(), CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_OwnerWhilePending_HrWhenApproved_EmployeeNotWhenApproved()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        var pending = await s.ApplyAsync(fromDay: 5, toDay: 5);
        Assert.Equal("Cancelled", (await s.Service.CancelAsync(pending.Id, new ReviewLeaveRequestDto(), CancellationToken.None)).Status);

        var second = await s.ApplyAsync(fromDay: 11, toDay: 11);
        s.User.SignInAs(UserRole.HR);
        await s.Service.ApproveAsync(second.Id, new ReviewLeaveRequestDto(), CancellationToken.None);

        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CancelAsync(second.Id, new ReviewLeaveRequestDto(), CancellationToken.None));

        s.User.SignInAs(UserRole.HR);
        Assert.Equal("Cancelled", (await s.Service.CancelAsync(second.Id, new ReviewLeaveRequestDto(), CancellationToken.None)).Status);
    }

    [Fact]
    public async Task LockedPayroll_BlocksApproval()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        var request = await s.ApplyAsync();
        s.Payroll.Periods.Add(new PayrollPeriod { StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31), Status = PayrollPeriodStatus.Approved });

        s.User.SignInAs(UserRole.HR);
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.ApproveAsync(request.Id, new ReviewLeaveRequestDto(), CancellationToken.None));
    }

    [Fact]
    public async Task Visibility_EmployeeSeesOwn_ManagerSeesTeam_AllScopeIsHrOnly()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        await s.ApplyAsync();
        s.User.SignInAs(UserRole.Employee, s.Other.Id);
        await s.ApplyAsync();

        s.User.SignInAs(UserRole.Employee, s.Report.Id);
        Assert.Single(await s.Service.GetAllAsync(new LeaveQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetAllAsync(new LeaveQueryDto { Scope = "all" }, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetAllAsync(new LeaveQueryDto { EmployeeId = s.Other.Id }, CancellationToken.None));

        s.User.SignInAs(UserRole.Manager, s.Manager.Id);
        var team = await s.Service.GetAllAsync(new LeaveQueryDto { Scope = "team" }, CancellationToken.None);
        Assert.Equal(s.Report.Id, Assert.Single(team).EmployeeId);
        Assert.True(team[0].CanReview);

        s.User.SignInAs(UserRole.HR);
        Assert.Equal(2, (await s.Service.GetAllAsync(new LeaveQueryDto { Scope = "all" }, CancellationToken.None)).Count);
    }
}
