using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>What rounding a day would do: the rounded tasks and the tasks that had to be skipped.</summary>
public sealed record RoundingPlan(IReadOnlyList<RoundedTask> Tasks, IReadOnlyList<SkippedTask> Skipped)
{
    public static readonly RoundingPlan Empty = new([], []);

    /// <summary>Every session change of the rounded tasks, in task order.</summary>
    public IReadOnlyList<SessionChange> Changes { get; } = [.. Tasks.SelectMany(t => t.Changes)];

    /// <summary>True when some task of the day is not on a half hour.</summary>
    public bool HasWork => Tasks.Count > 0 || Skipped.Count > 0;
}
