using System.ComponentModel.DataAnnotations;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Attendances.Dtos;

/// <summary>
/// HR correction of an attendance record. Full update: omitted times and remarks are cleared. The employee and the
/// date can't be changed (delete and re-create instead); working minutes are recalculated by the server.
/// </summary>
public class UpdateAttendanceDto
{
    [Required]
    [EnumDataType(typeof(AttendanceStatus))]
    public AttendanceStatus? Status { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}
