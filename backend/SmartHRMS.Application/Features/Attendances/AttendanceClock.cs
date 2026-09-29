using System.Globalization;

namespace smartHRMS.Application.Features.Attendances;

/// <summary>
/// The single place that turns "now" into office-local attendance values. Everything attendance-related asks this
/// class for today's date and the current time, so the server's own time zone never leaks into attendance records.
/// </summary>
public sealed class AttendanceClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public AttendanceClock(TimeProvider timeProvider, AttendanceOptions options)
    {
        EnsureValid(options);
        _timeProvider = timeProvider;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        WorkdayStart = ParseWorkdayStart(options.WorkdayStartTime);
        LateAfter = WorkdayStart.AddMinutes(options.LateGraceMinutes);
    }

    public TimeOnly WorkdayStart { get; }

    /// <summary>A check-in later than this is Late.</summary>
    public TimeOnly LateAfter { get; }

    /// <summary>The current moment in the office time zone.</summary>
    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);

    /// <summary>Today's date in the office time zone.</summary>
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>The current office wall-clock time, to the second (the precision attendance is stored with).</summary>
    public TimeOnly CurrentTime => ToSeconds(TimeOnly.FromDateTime(Now.DateTime));

    public bool IsLate(TimeOnly checkInTime) => checkInTime > LateAfter;

    public static TimeOnly ToSeconds(TimeOnly time) => new(time.Hour, time.Minute, time.Second);

    /// <summary>Fails fast with a clear message when the configuration can't be used.</summary>
    public static void EnsureValid(AttendanceOptions options)
    {
        var section = AttendanceOptions.SectionName;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new InvalidOperationException($"{section}:TimeZone '{options.TimeZone}' is not a known time zone.", exception);
        }

        var start = ParseWorkdayStart(options.WorkdayStartTime);

        if (options.LateGraceMinutes < 0 || start.ToTimeSpan().TotalMinutes + options.LateGraceMinutes >= 24 * 60)
        {
            throw new InvalidOperationException(
                $"{section}:LateGraceMinutes must be 0 or more and keep the late threshold within the same day.");
        }
    }

    private static TimeOnly ParseWorkdayStart(string value)
    {
        return TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            ? start
            : throw new InvalidOperationException($"{AttendanceOptions.SectionName}:WorkdayStartTime must be \"HH:mm\", e.g. \"09:00\".");
    }
}
