using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Attendances.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Attendances;

public class AttendanceServiceTests
{
    // Office time zone is Asia/Dhaka (UTC+6): 03:05 UTC is 09:05 in the office.
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly DateTimeOffset At0905Office = new(2026, 9, 29, 3, 5, 0, TimeSpan.Zero);

    private sealed record Setup(
        AttendanceService Service,
        FakeAttendanceRepository Attendance,
        FakeTimeProvider Clock,
        Employee Active,
        Employee Inactive,
        Employee OnLeave);

    private static Setup CreateService(DateTimeOffset? utcNow = null)
    {
        var employees = new FakeEmployeeRepository();
        var active = new Employee { EmployeeCode = "EMP-001", FirstName = "Jane", LastName = "Doe", Status = EmployeeStatus.Active, JoiningDate = new DateTime(2020, 1, 1) };
        var inactive = new Employee { EmployeeCode = "EMP-002", FirstName = "Old", LastName = "Timer", Status = EmployeeStatus.Inactive, JoiningDate = new DateTime(2020, 1, 1) };
        var onLeave = new Employee { EmployeeCode = "EMP-003", FirstName = "Away", LastName = "Now", Status = EmployeeStatus.OnLeave, JoiningDate = new DateTime(2020, 1, 1) };
        employees.Employees.AddRange(new[] { active, inactive, onLeave });

        var attendance = new FakeAttendanceRepository(employees);
        var clock = new FakeTimeProvider(utcNow ?? At0905Office);
        var options = new AttendanceOptions { TimeZone = "Asia/Dhaka", WorkdayStartTime = "09:00", LateGraceMinutes = 15 };

        return new Setup(new AttendanceService(attendance, employees, new AttendanceClock(clock, options)), attendance, clock, active, inactive, onLeave);
    }

    private static CreateAttendanceDto Create(Guid employeeId, DateOnly? date = null, AttendanceStatus status = AttendanceStatus.Present, TimeOnly? checkIn = null, TimeOnly? checkOut = null)
    {
        return new CreateAttendanceDto { EmployeeId = employeeId, AttendanceDate = date ?? Today, Status = status, CheckInTime = checkIn, CheckOutTime = checkOut };
    }

    // ---- employee validation ----

    [Fact]
    public async Task CreateAsync_ForExistingActiveEmployee_CreatesRecord()
    {
        var s = CreateService();

        var dto = Create(s.Active.Id, status: AttendanceStatus.Absent);
        dto.Remarks = "  Sick  ";

        var result = await s.Service.CreateAsync(dto, CancellationToken.None);

        var stored = Assert.Single(s.Attendance.Records);
        Assert.Equal(Today, stored.AttendanceDate);
        Assert.Equal(AttendanceStatus.Absent, stored.Status);
        Assert.Equal("EMP-001", result.EmployeeCode);
        Assert.Equal("Jane Doe", result.EmployeeName);
        Assert.Equal("Absent", result.Status);
        Assert.Equal("Sick", result.Remarks);
        Assert.Null(result.WorkingMinutes);
    }

    [Fact]
    public async Task CreateAsync_ForUnknownEmployee_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CreateAsync(Create(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ForInactiveEmployee_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreateAsync(Create(s.Inactive.Id, status: AttendanceStatus.Absent), CancellationToken.None));
        Assert.Empty(s.Attendance.Records);
    }

    [Fact]
    public async Task CreateAsync_ForOnLeaveEmployee_IsAllowed()
    {
        var s = CreateService();

        var result = await s.Service.CreateAsync(Create(s.OnLeave.Id, status: AttendanceStatus.Leave), CancellationToken.None);

        Assert.Equal("Leave", result.Status);
    }

    // ---- creation rules ----

    [Fact]
    public async Task CreateAsync_SameEmployeeAndDate_ThrowsConflict()
    {
        var s = CreateService();
        await s.Service.CreateAsync(Create(s.Active.Id, status: AttendanceStatus.Absent), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CreateAsync(Create(s.Active.Id, status: AttendanceStatus.Leave), CancellationToken.None));
        Assert.Single(s.Attendance.Records);
    }

    [Fact]
    public async Task CreateAsync_FutureDate_IsOnlyAllowedForLeave()
    {
        var s = CreateService();
        var tomorrow = Today.AddDays(1);

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreateAsync(Create(s.Active.Id, tomorrow, AttendanceStatus.Absent), CancellationToken.None));
        var leave = await s.Service.CreateAsync(Create(s.Active.Id, tomorrow, AttendanceStatus.Leave), CancellationToken.None);

        Assert.Equal(tomorrow, leave.AttendanceDate);
    }

    [Fact]
    public async Task CreateAsync_BeforeJoiningDate_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.CreateAsync(Create(s.Active.Id, new DateOnly(2019, 12, 31), AttendanceStatus.Absent), CancellationToken.None));
    }

    [Theory]
    [InlineData(AttendanceStatus.Absent)]
    [InlineData(AttendanceStatus.Leave)]
    public async Task CreateAsync_AbsentOrLeaveWithTimes_ThrowsBadRequest(AttendanceStatus status)
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.CreateAsync(Create(s.Active.Id, Today.AddDays(-1), status, new TimeOnly(9, 0)), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithTimes_CalculatesWorkingMinutes()
    {
        var s = CreateService();

        var result = await s.Service.CreateAsync(
            Create(s.Active.Id, Today.AddDays(-1), AttendanceStatus.Present, new TimeOnly(9, 0), new TimeOnly(17, 30)), CancellationToken.None);

        Assert.Equal(510, result.WorkingMinutes);
        Assert.Equal(new TimeOnly(9, 0), result.CheckInTime);
        Assert.Equal(new TimeOnly(17, 30), result.CheckOutTime);
    }

    [Fact]
    public async Task CreateAsync_CheckOutBeforeCheckIn_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreateAsync(
            Create(s.Active.Id, Today.AddDays(-1), AttendanceStatus.Present, new TimeOnly(17, 0), new TimeOnly(9, 0)), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_CheckOutWithoutCheckIn_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreateAsync(
            Create(s.Active.Id, Today.AddDays(-1), AttendanceStatus.Present, checkOut: new TimeOnly(17, 0)), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_TodayWithTimeInTheFuture_ThrowsBadRequest()
    {
        var s = CreateService(); // it is 09:05 in the office

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CreateAsync(
            Create(s.Active.Id, Today, AttendanceStatus.Present, new TimeOnly(10, 0)), CancellationToken.None));
    }

    // ---- check-in ----

    [Fact]
    public async Task CheckInAsync_BeforeLateThreshold_CreatesPresentRecordAtOfficeTime()
    {
        var s = CreateService(); // 09:05, threshold 09:15

        var result = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Equal(Today, result.AttendanceDate);
        Assert.Equal(new TimeOnly(9, 5), result.CheckInTime);
        Assert.Equal("Present", result.Status);
        Assert.Null(result.CheckOutTime);
        Assert.Null(result.WorkingMinutes);
    }

    [Fact]
    public async Task CheckInAsync_AfterLateThreshold_IsLate()
    {
        var s = CreateService(new DateTimeOffset(2026, 9, 29, 3, 16, 0, TimeSpan.Zero)); // 09:16

        var result = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Equal("Late", result.Status);
    }

    [Fact]
    public async Task CheckInAsync_ExactlyAtThreshold_IsStillPresent()
    {
        var s = CreateService(new DateTimeOffset(2026, 9, 29, 3, 15, 0, TimeSpan.Zero)); // 09:15

        var result = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Equal("Present", result.Status);
    }

    [Fact]
    public async Task CheckInAsync_UsesTheOfficeDateNotTheUtcDate()
    {
        // 19:30 UTC on the 29th is 01:30 on the 30th in Dhaka.
        var s = CreateService(new DateTimeOffset(2026, 9, 29, 19, 30, 0, TimeSpan.Zero));

        var result = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 30), result.AttendanceDate);
        Assert.Equal(new TimeOnly(1, 30), result.CheckInTime);
    }

    [Fact]
    public async Task CheckInAsync_Twice_ThrowsConflict()
    {
        var s = CreateService();
        await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None));
        Assert.Single(s.Attendance.Records);
    }

    [Fact]
    public async Task CheckInAsync_UnknownEmployee_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CheckInAsync(new CheckInDto { EmployeeId = Guid.NewGuid() }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckInAsync_InactiveEmployee_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Inactive.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckInAsync_ForAnotherDate_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id, AttendanceDate = Today.AddDays(-1) }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckInAsync_WhenMarkedAsLeave_ThrowsConflict()
    {
        var s = CreateService();
        await s.Service.CreateAsync(Create(s.Active.Id, status: AttendanceStatus.Leave), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckInAsync_OnAnAbsentRecord_FillsItIn()
    {
        var s = CreateService();
        await s.Service.CreateAsync(Create(s.Active.Id, status: AttendanceStatus.Absent), CancellationToken.None);

        var result = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Single(s.Attendance.Records);
        Assert.Equal("Present", result.Status);
        Assert.Equal(new TimeOnly(9, 5), result.CheckInTime);
    }

    // ---- check-out ----

    [Fact]
    public async Task CheckOutAsync_CalculatesWorkingMinutes()
    {
        var s = CreateService(new DateTimeOffset(2026, 9, 29, 3, 0, 0, TimeSpan.Zero)); // 09:00
        await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);
        s.Clock.UtcNow = new DateTimeOffset(2026, 9, 29, 11, 30, 0, TimeSpan.Zero); // 17:30

        var result = await s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Equal(new TimeOnly(17, 30), result.CheckOutTime);
        Assert.Equal(510, result.WorkingMinutes);
    }

    [Fact]
    public async Task CheckOutAsync_WithoutAttendance_ThrowsBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = s.Active.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckOutAsync_WithoutCheckIn_ThrowsBadRequest()
    {
        var s = CreateService();
        await s.Service.CreateAsync(Create(s.Active.Id, status: AttendanceStatus.Absent), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = s.Active.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckOutAsync_Twice_ThrowsConflict()
    {
        var s = CreateService();
        await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);
        await s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = s.Active.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task CheckOutAsync_EarlierThanCheckIn_ThrowsBadRequest()
    {
        var s = CreateService(); // 09:05
        s.Attendance.Records.Add(new Attendance { EmployeeId = s.Active.Id, AttendanceDate = Today, CheckInTime = new TimeOnly(10, 0), Status = AttendanceStatus.Present });

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = s.Active.Id }, CancellationToken.None));
        Assert.Null(s.Attendance.Records.Single().CheckOutTime);
    }

    [Fact]
    public async Task CheckOutAsync_UnknownEmployee_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.CheckOutAsync(new CheckOutDto { EmployeeId = Guid.NewGuid() }, CancellationToken.None));
    }

    // ---- retrieval ----

    [Fact]
    public async Task GetByIdAsync_ReturnsRecord_OrThrowsNotFound()
    {
        var s = CreateService();
        var created = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        Assert.Equal(created.Id, (await s.Service.GetByIdAsync(created.Id, CancellationToken.None)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByEmployeeAsync_ReturnsOnlyThatEmployee_NewestFirst()
    {
        var s = await SeedAsync();

        var history = await s.Service.GetByEmployeeAsync(s.Active.Id, new AttendanceQueryDto(), CancellationToken.None);

        Assert.Equal(new[] { Today, Today.AddDays(-1), Today.AddDays(-2) }, history.Select(a => a.AttendanceDate));
        Assert.All(history, a => Assert.Equal(s.Active.Id, a.EmployeeId));
    }

    [Fact]
    public async Task GetByEmployeeAsync_UnknownEmployee_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetByEmployeeAsync(Guid.NewGuid(), new AttendanceQueryDto(), CancellationToken.None));
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDate()
    {
        var s = await SeedAsync();

        var result = await s.Service.GetAllAsync(new AttendanceListQueryDto { Date = Today }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(Today, a.AttendanceDate));
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatusCaseInsensitively()
    {
        var s = await SeedAsync();

        var result = await s.Service.GetAllAsync(new AttendanceListQueryDto { Status = "absent" }, CancellationToken.None);

        Assert.Equal("Absent", Assert.Single(result).Status);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDateRangeAndEmployee()
    {
        var s = await SeedAsync();

        var result = await s.Service.GetAllAsync(
            new AttendanceListQueryDto { EmployeeId = s.Active.Id, StartDate = Today.AddDays(-2), EndDate = Today.AddDays(-1) }, CancellationToken.None);

        Assert.Equal(new[] { Today.AddDays(-1), Today.AddDays(-2) }, result.Select(a => a.AttendanceDate));
    }

    [Theory]
    [InlineData("Holiday")]
    [InlineData("1")]
    public async Task GetAllAsync_InvalidStatus_ThrowsBadRequest(string status)
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.GetAllAsync(new AttendanceListQueryDto { Status = status }, CancellationToken.None));
    }

    [Fact]
    public async Task GetAllAsync_InvalidRanges_ThrowBadRequest()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.GetAllAsync(new AttendanceListQueryDto { StartDate = Today, EndDate = Today.AddDays(-1) }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.GetAllAsync(new AttendanceListQueryDto { Date = Today, StartDate = Today }, CancellationToken.None));
    }

    // ---- update / delete ----

    [Fact]
    public async Task UpdateAsync_RecalculatesWorkingMinutes_AndKeepsEmployeeAndDate()
    {
        var s = CreateService();
        var created = await s.Service.CreateAsync(Create(s.Active.Id, Today.AddDays(-1), AttendanceStatus.Absent), CancellationToken.None);

        var result = await s.Service.UpdateAsync(
            created.Id,
            new UpdateAttendanceDto { Status = AttendanceStatus.HalfDay, CheckInTime = new TimeOnly(9, 0), CheckOutTime = new TimeOnly(13, 0), Remarks = " Doctor " },
            CancellationToken.None);

        Assert.Equal("HalfDay", result.Status);
        Assert.Equal(240, result.WorkingMinutes);
        Assert.Equal("Doctor", result.Remarks);
        Assert.Equal(s.Active.Id, result.EmployeeId);
        Assert.Equal(Today.AddDays(-1), result.AttendanceDate);
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_InvalidTimes_ThrowBadRequest_AndChangeNothing()
    {
        var s = CreateService();
        var created = await s.Service.CreateAsync(Create(s.Active.Id, Today.AddDays(-1), AttendanceStatus.Absent), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.UpdateAsync(
            created.Id, new UpdateAttendanceDto { Status = AttendanceStatus.Present, CheckInTime = new TimeOnly(17, 0), CheckOutTime = new TimeOnly(9, 0) }, CancellationToken.None));

        Assert.Equal(AttendanceStatus.Absent, s.Attendance.Records.Single().Status);
    }

    [Fact]
    public async Task UpdateAsync_UnknownRecord_ThrowsNotFound()
    {
        var s = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.Service.UpdateAsync(Guid.NewGuid(), new UpdateAttendanceDto { Status = AttendanceStatus.Present }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecord_UnknownThrowsNotFound()
    {
        var s = CreateService();
        var created = await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);

        await s.Service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Empty(s.Attendance.Records);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.DeleteAsync(created.Id, CancellationToken.None));
    }

    // ---- configuration ----

    [Theory]
    [InlineData("Mars/Olympus", "09:00", 15)]
    [InlineData("Asia/Dhaka", "9am", 15)]
    [InlineData("Asia/Dhaka", "23:50", 15)]
    [InlineData("Asia/Dhaka", "09:00", -1)]
    public void EnsureValid_RejectsUnusableConfiguration(string timeZone, string start, int grace)
    {
        Assert.Throws<InvalidOperationException>(() =>
            AttendanceClock.EnsureValid(new AttendanceOptions { TimeZone = timeZone, WorkdayStartTime = start, LateGraceMinutes = grace }));
    }

    /// <summary>Active: today (check-in), yesterday (Absent), two days ago (Present with times); on-leave employee: today (Leave).</summary>
    private static async Task<Setup> SeedAsync()
    {
        var s = CreateService();
        await s.Service.CheckInAsync(new CheckInDto { EmployeeId = s.Active.Id }, CancellationToken.None);
        await s.Service.CreateAsync(Create(s.Active.Id, Today.AddDays(-1), AttendanceStatus.Absent), CancellationToken.None);
        await s.Service.CreateAsync(Create(s.Active.Id, Today.AddDays(-2), AttendanceStatus.Present, new TimeOnly(9, 0), new TimeOnly(18, 0)), CancellationToken.None);
        await s.Service.CreateAsync(Create(s.OnLeave.Id, Today, AttendanceStatus.Leave), CancellationToken.None);
        return s;
    }
}
