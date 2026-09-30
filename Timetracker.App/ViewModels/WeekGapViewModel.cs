using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

/// <summary>
/// One untracked gap inside an expanded day of the week tree, e.g.
/// "⚠ 10:15–11:05 untracked (0:50)"; the view offers to book it as a session or to
/// distribute it over the sessions bordering it.
/// </summary>
public sealed class WeekGapViewModel
{
    public WeekGapViewModel(TimeRange range, IReadOnlyList<SessionChange> distribution)
    {
        Range = range;
        Distribution = distribution;
    }

    /// <summary>The untracked stretch.</summary>
    public TimeRange Range { get; }

    /// <summary>
    /// Changes that stretch the bordering sessions over the gap (see
    /// <see cref="GapDistribution"/>); empty when no session touches it.
    /// </summary>
    public IReadOnlyList<SessionChange> Distribution { get; }

    /// <summary>True when a session borders the gap, so it can absorb it.</summary>
    public bool CanDistribute => Distribution.Count > 0;

    /// <summary>
    /// One line per stretched session for the confirmation, e.g.
    /// "Report 09:00–10:15 → 09:00–10:44".
    /// </summary>
    public IReadOnlyList<string> DistributionLines =>
        [.. Distribution.Select(c =>
            $"{c.Original.Task} {Clock(c.Original.Start, c.Original.End)} → "
            + (c.Updated is { } updated ? Clock(updated.Start, updated.End) : "removed"))];

    /// <summary>Tooltip for the distribute action.</summary>
    public string DistributionHint => CanDistribute
        ? "Stretch " + string.Join(" and ", Distribution.Select(c => $"\"{c.Original.Task}\"")) + " over this gap"
        : "No session starts or ends at this gap, so nothing can absorb it; book it instead";

    /// <summary>Clock range, e.g. "10:15–11:05".</summary>
    public string TimeText => Clock(Range.Start, Range.End);

    /// <summary>Length as "h:mm", e.g. "0:50".</summary>
    public string DurationText => WeekTimeFormat.HoursMinutes(Range.Duration.TotalSeconds);

    /// <summary>Row caption, e.g. "⚠ 10:15–11:05 untracked (0:50)".</summary>
    public string Text => $"⚠ {TimeText} untracked ({DurationText})";

    private static string Clock(DateTimeOffset start, DateTimeOffset end) => $"{start:HH\\:mm}–{end:HH\\:mm}";
}
