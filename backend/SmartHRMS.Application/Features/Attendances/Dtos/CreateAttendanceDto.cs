using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Attendances.Dtos;

/// <summary>
/// A manual attendance record entered by HR (e.g. Absent, Leave, HalfDay, or a missed check-in). Working minutes are
/// always calculated by the server.
/// </summary>
public class CreateAttendanceDto
{
    [NotDefault]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateOnly? AttendanceDate { get; set; }

    [Required]
    [EnumDataType(typeof(AttendanceStatus))]
    public AttendanceStatus? Status { get; set; }

    /// <summary>Optional office-local time. Not allowed for Absent or Leave.</summary>
    public TimeOnly? CheckInTime { get; set; }

    /// <summary>Optional; requires a check-in time and can't be earlier than it.</summary>
    public TimeOnly? CheckOutTime { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}
