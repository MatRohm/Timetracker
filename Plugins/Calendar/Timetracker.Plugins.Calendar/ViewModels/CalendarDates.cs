namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>Date arithmetic shared by the calendar views.</summary>
public static class CalendarDates
{
    /// <summary>Monday 00:00 of the week containing <paramref name="moment"/>.</summary>
    public static DateTimeOffset StartOfWeek(DateTimeOffset moment)
    {
        var back = ((int)moment.Date.DayOfWeek + 6) % 7; // Sunday=0 -> 6 days back
        var result = new DateTimeOffset(moment.Date.AddDays(-back), moment.Offset);
        return result;
    }

    /// <summary>First day of <paramref name="moment"/>'s month at 00:00.</summary>
    public static DateTimeOffset FirstOfMonth(DateTimeOffset moment) =>
        new(moment.Year, moment.Month, 1, 0, 0, 0, moment.Offset);
}
