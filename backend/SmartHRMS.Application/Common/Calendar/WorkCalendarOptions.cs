namespace smartHRMS.Application.Common.Calendar;

/// <summary>The office working week, bound from the "WorkCalendar" section. Used by leave and payroll day counts.</summary>
public class WorkCalendarOptions
{
    public const string SectionName = "WorkCalendar";

    /// <summary>Weekly days off (DayOfWeek names). Public holidays are not modelled.</summary>
    public string[] WeekendDays { get; set; } = ["Friday", "Saturday"];
}
