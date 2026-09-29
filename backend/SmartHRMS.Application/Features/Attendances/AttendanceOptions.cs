namespace smartHRMS.Application.Features.Attendances;

/// <summary>
/// Attendance rules, bound from the "Attendance" section of appsettings.json. Validated once at startup by
/// <see cref="AttendanceClock.EnsureValid"/>, so a bad time zone or start time stops the API instead of producing
/// wrong attendance dates later.
/// </summary>
public class AttendanceOptions
{
    public const string SectionName = "Attendance";

    /// <summary>
    /// Time zone of the office (IANA or Windows id). Attendance dates and check-in/out times are recorded in this zone,
    /// whatever the server's own time zone is.
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Dhaka";

    /// <summary>Start of the working day, "HH:mm" in the office time zone.</summary>
    public string WorkdayStartTime { get; set; } = "09:00";

    /// <summary>Minutes after the workday start that still count as on time. A later check-in is Late.</summary>
    public int LateGraceMinutes { get; set; } = 15;
}
