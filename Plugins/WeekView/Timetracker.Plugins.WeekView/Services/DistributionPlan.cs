using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>What distributing a day's missing time over its tasks would do.</summary>
public sealed record DistributionPlan(IReadOnlyList<DistributedTask> Tasks)
{
    public static readonly DistributionPlan Empty = new([]);

    /// <summary>Every session change of the distributed tasks, in task order.</summary>
    public IReadOnlyList<SessionChange> Changes { get; } = [.. Tasks.SelectMany(t => t.Changes)];

    /// <summary>True when some task of the day can take a share.</summary>
    public bool HasWork => Tasks.Count > 0;
}
