using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Tests.Fakes;

public class FakeAttendanceRepository : IAttendanceRepository
{
    private readonly FakeEmployeeRepository _employees;

    public FakeAttendanceRepository(FakeEmployeeRepository employees)
    {
        _employees = employees;
    }

    public List<Attendance> Records { get; } = new();

    public Task<Attendance?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(WithEmployee(Records.FirstOrDefault(a => a.Id == id)));
    }

    public Task<Attendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken)
    {
        return Task.FromResult(WithEmployee(Records.FirstOrDefault(a => a.EmployeeId == employeeId && a.AttendanceDate == attendanceDate)));
    }

    public Task<List<Attendance>> SearchAsync(AttendanceFilter filter, CancellationToken cancellationToken)
    {
        var results = Records
            .Where(a => filter.EmployeeId is null || a.EmployeeId == filter.EmployeeId)
            .Where(a => filter.From is null || a.AttendanceDate >= filter.From)
            .Where(a => filter.To is null || a.AttendanceDate <= filter.To)
            .Where(a => filter.Status is null || a.Status == filter.Status)
            .Select(a => WithEmployee(a)!)
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.Employee?.EmployeeCode)
            .ToList();
        return Task.FromResult(results);
    }

    public Task AddAsync(Attendance attendance, CancellationToken cancellationToken)
    {
        Records.Add(attendance);
        return Task.CompletedTask;
    }

    public void Remove(Attendance attendance)
    {
        Records.Remove(attendance);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // Mirrors the real repository's Include(a => a.Employee).
    private Attendance? WithEmployee(Attendance? attendance)
    {
        if (attendance is not null)
        {
            attendance.Employee ??= _employees.Employees.FirstOrDefault(e => e.Id == attendance.EmployeeId);
        }

        return attendance;
    }
}
