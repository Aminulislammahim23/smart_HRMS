using smartHRMS.Application.Features.Attendances.Dtos;

namespace smartHRMS.Application.Features.Attendances;

public interface IAttendanceService
{
    Task<List<AttendanceDto>> GetAllAsync(AttendanceListQueryDto query, CancellationToken cancellationToken);

    /// <summary>Attendance history of one employee (404 if the employee doesn't exist).</summary>
    Task<List<AttendanceDto>> GetByEmployeeAsync(Guid employeeId, AttendanceQueryDto query, CancellationToken cancellationToken);

    Task<AttendanceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AttendanceDto> CreateAsync(CreateAttendanceDto dto, CancellationToken cancellationToken);

    Task<AttendanceDto> UpdateAsync(Guid id, UpdateAttendanceDto dto, CancellationToken cancellationToken);

    /// <summary>Permanently removes the record (HR correction).</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<AttendanceDto> CheckInAsync(CheckInDto dto, CancellationToken cancellationToken);

    Task<AttendanceDto> CheckOutAsync(CheckOutDto dto, CancellationToken cancellationToken);
}
