using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Localization;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// One untracked gap inside an expanded day of the week tree, e.g.
/// "⚠ 10:15–11:05 untracked (0:50)"; the view offers to book it as a session.
/// </summary>
public sealed class WeekGapViewModel(TimeRange range)
{

    /// <summary>The untracked stretch.</summary>
    public TimeRange Range { get; } = range;

    /// <summary>Clock range, e.g. "10:15–11:05".</summary>
    public string TimeText => Clock(Range.Start, Range.End);

    /// <summary>Length as "h:mm", e.g. "0:50".</summary>
    public string DurationText => WeekTimeFormat.HoursMinutes(Range.Duration.TotalSeconds);

    /// <summary>Row caption, e.g. "⚠ 10:15–11:05 untracked (0:50)".</summary>
    public string Text => string.Format(Strings.Week_GapWarning, TimeText, DurationText);

    private static string Clock(DateTimeOffset start, DateTimeOffset end) => WeekTimeFormat.ClockRange(start, end);
}
