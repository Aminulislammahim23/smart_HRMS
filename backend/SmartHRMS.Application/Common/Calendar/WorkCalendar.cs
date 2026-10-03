namespace smartHRMS.Application.Common.Calendar;

/// <summary>Decides which dates are working days. Weekends come from configuration; holidays are not modelled.</summary>
public sealed class WorkCalendar
{
    private readonly HashSet<DayOfWeek> _weekend;

    public WorkCalendar(WorkCalendarOptions options)
    {
        _weekend = Parse(options);
    }

    public bool IsWorkingDay(DateOnly date) => !_weekend.Contains(date.DayOfWeek);

    /// <summary>Working days from <paramref name="from"/> to <paramref name="to"/>, both inclusive (none when from &gt; to).</summary>
    public IEnumerable<DateOnly> WorkingDays(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (IsWorkingDay(date))
            {
                yield return date;
            }
        }
    }

    public int CountWorkingDays(DateOnly from, DateOnly to) => WorkingDays(from, to).Count();

    /// <summary>Fails fast at startup when the configured weekend can't be used.</summary>
    public static void EnsureValid(WorkCalendarOptions options) => Parse(options);

    private static HashSet<DayOfWeek> Parse(WorkCalendarOptions options)
    {
        var days = new HashSet<DayOfWeek>();
        foreach (var name in options.WeekendDays ?? [])
        {
            if (int.TryParse(name, out _) || !Enum.TryParse<DayOfWeek>(name?.Trim(), ignoreCase: true, out var day) || !Enum.IsDefined(day))
            {
                throw new InvalidOperationException($"{WorkCalendarOptions.SectionName}:WeekendDays contains '{name}', which is not a day name.");
            }

            days.Add(day);
        }

        if (days.Count >= 7)
        {
            throw new InvalidOperationException($"{WorkCalendarOptions.SectionName}:WeekendDays must leave at least one working day.");
        }

        return days;
    }
}
