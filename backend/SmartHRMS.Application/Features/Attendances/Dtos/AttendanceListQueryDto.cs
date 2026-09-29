namespace smartHRMS.Application.Features.Attendances.Dtos;

/// <summary>Filters for GET /api/attendance: the common ones plus an optional employee.</summary>
public class AttendanceListQueryDto : AttendanceQueryDto
{
    public Guid? EmployeeId { get; set; }
}
