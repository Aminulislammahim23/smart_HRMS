using smartHRMS.Application.Features.Attendances;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Application.Interfaces;

public interface IAttendanceRepository
{
    /// <summary>Tracked, with the employee loaded.</summary>
    Task<Attendance?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked, with the employee loaded. At most one record exists per employee and date.</summary>
    Task<Attendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken);

    /// <summary>Read-only, with the employee loaded; newest date first, then by employee code.</summary>
    Task<List<Attendance>> SearchAsync(AttendanceFilter filter, CancellationToken cancellationToken);

    Task AddAsync(Attendance attendance, CancellationToken cancellationToken);

    void Remove(Attendance attendance);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
