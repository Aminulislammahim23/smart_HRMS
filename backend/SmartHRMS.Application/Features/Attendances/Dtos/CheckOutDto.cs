using System.ComponentModel.DataAnnotations;
using smartHRMS.Application.Common.Validation;

namespace smartHRMS.Application.Features.Attendances.Dtos;

/// <summary>Self check-out for today's attendance, at the current server time. Same date rule as check-in.</summary>
public class CheckOutDto
{
    [NotDefault]
    public Guid EmployeeId { get; set; }

    public DateOnly? AttendanceDate { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}
