using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// One employee's attendance for one calendar day (at most one record per employee and date). Dates and times are
/// office-local wall-clock values in the configured attendance time zone, not UTC instants.
/// </summary>
public class Attendance : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    /// <summary>Calculated by the server from the check-in and check-out times; never taken from a client.</summary>
    public int? WorkingMinutes { get; set; }

    public AttendanceStatus Status { get; set; }

    public string? Remarks { get; set; }
}
