using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

/// <summary>
/// One untracked gap inside an expanded day of the week tree, e.g.
/// "⚠ 10:15–11:05 untracked (0:50)"; the view offers to book it as a session.
/// </summary>
public sealed class WeekGapViewModel
{
    public WeekGapViewModel(TimeRange range)
    {
        Range = range;
    }

    /// <summary>The untracked stretch.</summary>
    public TimeRange Range { get; }

    /// <summary>Clock range, e.g. "10:15–11:05".</summary>
    public string TimeText => $"{Range.Start:HH\\:mm}–{Range.End:HH\\:mm}";

    /// <summary>Length as "h:mm", e.g. "0:50".</summary>
    public string DurationText => WeekTimeFormat.HoursMinutes(Range.Duration.TotalSeconds);

    /// <summary>Row caption, e.g. "⚠ 10:15–11:05 untracked (0:50)".</summary>
    public string Text => $"⚠ {TimeText} untracked ({DurationText})";
}
