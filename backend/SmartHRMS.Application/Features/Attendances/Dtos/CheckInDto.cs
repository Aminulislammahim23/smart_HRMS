using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;

namespace smartHRMS.Application.Features.Attendances.Dtos;

/// <summary>
/// Self check-in. The date and time always come from the server clock in the office time zone, so a check-in can't be
/// back-dated. <see cref="AttendanceDate"/> is optional; when sent it must be today (guards against a client that
/// thinks it is still yesterday, e.g. just after midnight).
/// </summary>
public class CheckInDto
{
    [NotDefault]
    public Guid EmployeeId { get; set; }

    public DateOnly? AttendanceDate { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}
