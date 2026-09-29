namespace smartHRMS.Application.Features.Attendances.Dtos;

public class AttendanceDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Calendar date in the office time zone ("2026-09-30").</summary>
    public DateOnly AttendanceDate { get; set; }

    /// <summary>Office-local wall-clock time ("09:05:00"), or null.</summary>
    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    /// <summary>checkOutTime − checkInTime in whole minutes; null until both are known.</summary>
    public int? WorkingMinutes { get; set; }

    /// <summary>Present, Late, Absent, HalfDay or Leave.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
