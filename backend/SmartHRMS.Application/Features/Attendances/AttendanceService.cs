using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Text;
using smartHRMS.Application.Features.Attendances.Dtos;
using smartHRMS.Application.Features.Employees;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Attendances;

/// <summary>
/// Attendance rules:
/// - one record per employee and date (409 on a duplicate; the database enforces it too);
/// - only current employees (Active or OnLeave) can check in or get a new record;
/// - check-in/out use the server clock in the office time zone and only ever apply to today;
/// - a check-in after the workday start + grace minutes is Late, otherwise Present;
/// - working minutes = check-out − check-in, always calculated here.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly AttendanceClock _clock;

    public AttendanceService(IAttendanceRepository attendanceRepository, IEmployeeRepository employeeRepository, AttendanceClock clock)
    {
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _clock = clock;
    }

    public async Task<List<AttendanceDto>> GetAllAsync(AttendanceListQueryDto query, CancellationToken cancellationToken)
    {
        var records = await _attendanceRepository.SearchAsync(BuildFilter(query, query.EmployeeId), cancellationToken);
        return records.Select(MapToDto).ToList();
    }

    public async Task<List<AttendanceDto>> GetByEmployeeAsync(Guid employeeId, AttendanceQueryDto query, CancellationToken cancellationToken)
    {
        await _employeeRepository.EnsureExistsAsync(employeeId, cancellationToken);

        var records = await _attendanceRepository.SearchAsync(BuildFilter(query, employeeId), cancellationToken);
        return records.Select(MapToDto).ToList();
    }

    public async Task<AttendanceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return MapToDto(await GetExistingAsync(id, cancellationToken));
    }

    public async Task<AttendanceDto> CreateAsync(CreateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var employee = await GetCurrentEmployeeAsync(dto.EmployeeId, cancellationToken);
        var date = dto.AttendanceDate!.Value;
        var status = dto.Status!.Value;
        var checkIn = Truncate(dto.CheckInTime);
        var checkOut = Truncate(dto.CheckOutTime);

        ValidateDate(employee, date, status);
        ValidateTimes(date, status, checkIn, checkOut);
        await EnsureNoRecordAsync(employee, date, cancellationToken);

        var attendance = new Attendance
        {
            EmployeeId = employee.Id,
            Employee = employee,
            AttendanceDate = date,
            Status = status,
            CheckInTime = checkIn,
            CheckOutTime = checkOut,
            WorkingMinutes = CalculateWorkingMinutes(checkIn, checkOut),
            Remarks = InputText.Optional(dto.Remarks),
        };

        await _attendanceRepository.AddAsync(attendance, cancellationToken);
        await _attendanceRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(attendance);
    }

    public async Task<AttendanceDto> UpdateAsync(Guid id, UpdateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var attendance = await GetExistingAsync(id, cancellationToken);
        var status = dto.Status!.Value;
        var checkIn = Truncate(dto.CheckInTime);
        var checkOut = Truncate(dto.CheckOutTime);

        // Corrections are allowed for employees who have since left: the record itself is history.
        ValidateDate(attendance.Employee!, attendance.AttendanceDate, status);
        ValidateTimes(attendance.AttendanceDate, status, checkIn, checkOut);

        attendance.Status = status;
        attendance.CheckInTime = checkIn;
        attendance.CheckOutTime = checkOut;
        attendance.WorkingMinutes = CalculateWorkingMinutes(checkIn, checkOut);
        attendance.Remarks = InputText.Optional(dto.Remarks);
        attendance.UpdatedAt = DateTime.UtcNow;

        await _attendanceRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(attendance);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var attendance = await GetExistingAsync(id, cancellationToken);

        _attendanceRepository.Remove(attendance);
        await _attendanceRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceDto> CheckInAsync(CheckInDto dto, CancellationToken cancellationToken)
    {
        var employee = await GetCurrentEmployeeAsync(dto.EmployeeId, cancellationToken);
        var today = EnsureToday(dto.AttendanceDate);
        var now = _clock.CurrentTime;

        if (today < DateOnly.FromDateTime(employee.JoiningDate))
        {
            throw new BadRequestException($"Employee '{employee.EmployeeCode}' joins on {employee.JoiningDate:yyyy-MM-dd} and can't check in before that.");
        }

        var onTimeStatus = _clock.IsLate(now) ? AttendanceStatus.Late : AttendanceStatus.Present;
        var attendance = await _attendanceRepository.GetByEmployeeAndDateAsync(employee.Id, today, cancellationToken);

        if (attendance is null)
        {
            attendance = new Attendance
            {
                EmployeeId = employee.Id,
                Employee = employee,
                AttendanceDate = today,
                CheckInTime = now,
                Status = onTimeStatus,
                Remarks = InputText.Optional(dto.Remarks),
            };
            await _attendanceRepository.AddAsync(attendance, cancellationToken);
        }
        else
        {
            if (attendance.CheckInTime is not null)
            {
                throw new ConflictException($"Employee '{employee.EmployeeCode}' already checked in on {today:yyyy-MM-dd} at {attendance.CheckInTime:HH:mm:ss}.");
            }

            if (attendance.Status == AttendanceStatus.Leave)
            {
                throw new ConflictException($"Employee '{employee.EmployeeCode}' is on leave on {today:yyyy-MM-dd}. Update the attendance record instead.");
            }

            // A record HR created in advance (Absent or HalfDay) gets the check-in; a planned half day stays a half day.
            attendance.CheckInTime = now;
            attendance.Status = attendance.Status == AttendanceStatus.HalfDay ? AttendanceStatus.HalfDay : onTimeStatus;
            attendance.Remarks = InputText.Optional(dto.Remarks) ?? attendance.Remarks;
            attendance.UpdatedAt = DateTime.UtcNow;
        }

        await _attendanceRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(attendance);
    }

    public async Task<AttendanceDto> CheckOutAsync(CheckOutDto dto, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(dto.EmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{dto.EmployeeId}' was not found.");
        var today = EnsureToday(dto.AttendanceDate);

        var attendance = await _attendanceRepository.GetByEmployeeAndDateAsync(employee.Id, today, cancellationToken);
        if (attendance?.CheckInTime is null)
        {
            throw new BadRequestException($"Employee '{employee.EmployeeCode}' has not checked in on {today:yyyy-MM-dd}. Check in first.");
        }

        if (attendance.CheckOutTime is not null)
        {
            throw new ConflictException($"Employee '{employee.EmployeeCode}' already checked out on {today:yyyy-MM-dd} at {attendance.CheckOutTime:HH:mm:ss}.");
        }

        var now = _clock.CurrentTime;
        if (now < attendance.CheckInTime)
        {
            throw new BadRequestException($"The check-out time ({now:HH:mm:ss}) cannot be earlier than the check-in time ({attendance.CheckInTime:HH:mm:ss}).");
        }

        attendance.CheckOutTime = now;
        attendance.WorkingMinutes = CalculateWorkingMinutes(attendance.CheckInTime, now);
        attendance.Remarks = InputText.Optional(dto.Remarks) ?? attendance.Remarks;
        attendance.UpdatedAt = DateTime.UtcNow;

        await _attendanceRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(attendance);
    }

    /// <summary>Whole minutes between check-in and check-out, or null until both exist.</summary>
    internal static int? CalculateWorkingMinutes(TimeOnly? checkIn, TimeOnly? checkOut)
    {
        // TimeOnly subtraction wraps around midnight, so use TimeSpans (callers guarantee checkOut >= checkIn).
        return checkIn is null || checkOut is null
            ? null
            : (int)(checkOut.Value.ToTimeSpan() - checkIn.Value.ToTimeSpan()).TotalMinutes;
    }

    private async Task<Attendance> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _attendanceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Attendance with id '{id}' was not found.");
    }

    /// <summary>The employee must exist (404) and still work here — Active or OnLeave (400).</summary>
    private async Task<Employee> GetCurrentEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee with id '{employeeId}' was not found.");

        if (!EmployeeStatusRules.CurrentStatuses.Contains(employee.Status))
        {
            throw new BadRequestException($"Employee '{employee.EmployeeCode}' is {employee.Status} and can't have new attendance recorded.");
        }

        return employee;
    }

    private async Task EnsureNoRecordAsync(Employee employee, DateOnly date, CancellationToken cancellationToken)
    {
        if (await _attendanceRepository.GetByEmployeeAndDateAsync(employee.Id, date, cancellationToken) is not null)
        {
            throw new ConflictException($"Attendance for employee '{employee.EmployeeCode}' on {date:yyyy-MM-dd} already exists.");
        }
    }

    /// <summary>Check-in/out always apply to today; a date sent by the client must agree with the server.</summary>
    private DateOnly EnsureToday(DateOnly? requestedDate)
    {
        var today = _clock.Today;
        if (requestedDate is not null && requestedDate != today)
        {
            throw new BadRequestException($"Check-in and check-out are only possible for today ({today:yyyy-MM-dd}). Ask HR to correct other dates.");
        }

        return today;
    }

    private void ValidateDate(Employee employee, DateOnly date, AttendanceStatus status)
    {
        if (date > _clock.Today && status != AttendanceStatus.Leave)
        {
            throw new BadRequestException("The attendance date cannot be in the future (only leave can be recorded in advance).");
        }

        if (date < DateOnly.FromDateTime(employee.JoiningDate))
        {
            throw new BadRequestException($"The attendance date cannot be before the joining date ({employee.JoiningDate:yyyy-MM-dd}).");
        }
    }

    private void ValidateTimes(DateOnly date, AttendanceStatus status, TimeOnly? checkIn, TimeOnly? checkOut)
    {
        if ((status is AttendanceStatus.Absent or AttendanceStatus.Leave) && (checkIn is not null || checkOut is not null))
        {
            throw new BadRequestException($"A {status} record can't have check-in or check-out times.");
        }

        if (checkOut is not null && checkIn is null)
        {
            throw new BadRequestException("A check-out time requires a check-in time.");
        }

        if (checkOut < checkIn)
        {
            throw new BadRequestException("The check-out time cannot be earlier than the check-in time.");
        }

        if (date == _clock.Today && (checkIn > _clock.CurrentTime || checkOut > _clock.CurrentTime))
        {
            throw new BadRequestException("Check-in and check-out times for today cannot be in the future.");
        }
    }

    private static TimeOnly? Truncate(TimeOnly? time) => time is null ? null : AttendanceClock.ToSeconds(time.Value);

    private static AttendanceFilter BuildFilter(AttendanceQueryDto query, Guid? employeeId)
    {
        if (query.Date is not null && (query.StartDate is not null || query.EndDate is not null))
        {
            throw new BadRequestException("Use either date or startDate/endDate, not both.");
        }

        var from = query.Date ?? query.StartDate;
        var to = query.Date ?? query.EndDate;
        if (from > to)
        {
            throw new BadRequestException("The startDate cannot be after the endDate.");
        }

        return new AttendanceFilter(employeeId, from, to, ParseStatus(query.Status));
    }

    private static AttendanceStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Names only, like the JSON enums elsewhere in the API: "3" would otherwise match whatever value it happens to be.
        if (int.TryParse(value, out _)
            || !Enum.TryParse<AttendanceStatus>(value.Trim(), ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            throw new BadRequestException($"'{value}' is not a valid attendance status. Allowed values: {string.Join(", ", Enum.GetNames<AttendanceStatus>())}.");
        }

        return status;
    }

    internal static AttendanceDto MapToDto(Attendance attendance)
    {
        return new AttendanceDto
        {
            Id = attendance.Id,
            EmployeeId = attendance.EmployeeId,
            EmployeeCode = attendance.Employee?.EmployeeCode ?? string.Empty,
            EmployeeName = attendance.Employee is null ? string.Empty : $"{attendance.Employee.FirstName} {attendance.Employee.LastName}",
            AttendanceDate = attendance.AttendanceDate,
            CheckInTime = attendance.CheckInTime,
            CheckOutTime = attendance.CheckOutTime,
            WorkingMinutes = attendance.WorkingMinutes,
            Status = attendance.Status.ToString(),
            Remarks = attendance.Remarks,
            CreatedAt = attendance.CreatedAt,
            UpdatedAt = attendance.UpdatedAt,
        };
    }
}
