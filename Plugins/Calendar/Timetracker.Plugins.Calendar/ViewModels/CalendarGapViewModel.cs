using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Calendar.Localization;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// One untracked gap in a day column, e.g. "⚠ 10:15–11:05 untracked (0:50)";
/// the view draws it as a band and offers to book it as a session.
/// </summary>
public sealed class CalendarGapViewModel(TimeRange range)
{

    /// <summary>The untracked stretch.</summary>
    public TimeRange Range { get; } = range;

    /// <summary>Clock range, e.g. "10:15–11:05".</summary>
    public string TimeText => Clock(Range.Start, Range.End);

    /// <summary>Length as "h:mm", e.g. "0:50".</summary>
    public string DurationText => CalendarTimeFormat.HoursMinutes(Range.Duration.TotalSeconds);

    /// <summary>Row caption, e.g. "⚠ 10:15–11:05 untracked (0:50)".</summary>
    public string Text => string.Format(Strings.Calendar_GapWarning, TimeText, DurationText);

    private static string Clock(DateTimeOffset start, DateTimeOffset end) => CalendarTimeFormat.ClockRange(start, end);
}
