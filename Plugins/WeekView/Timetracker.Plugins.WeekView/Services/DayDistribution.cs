using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>A task whose day total grows by its proportional share of the day's missing time.</summary>
public sealed record DistributedTask(string Name, TimeSpan Total, TimeSpan Share, IReadOnlyList<SessionChange> Changes);

/// <summary>A task's hand-assigned share of the day's missing time, e.g. from the distribute dialog.</summary>
public sealed record AssignedShare(string Task, TimeSpan Share);

/// <summary>A day's tracked task: its grouped name, summed total and its sessions (earliest end last).</summary>
internal readonly record struct DayTask(string Name, TimeSpan Total, IReadOnlyList<TrackedSession> Sessions);

/// <summary>What distributing a day's missing time over its tasks would do.</summary>
public sealed record DistributionPlan(IReadOnlyList<DistributedTask> Tasks)
{
    public static readonly DistributionPlan Empty = new([]);

    /// <summary>Every session change of the distributed tasks, in task order.</summary>
    public IReadOnlyList<SessionChange> Changes { get; } = [.. Tasks.SelectMany(t => t.Changes)];

    /// <summary>True when some task of the day can take a share.</summary>
    public bool HasWork => Tasks.Count > 0;
}

/// <summary>
/// Plans distributing a day's missing (untracked) time over its tasks, in
/// proportion to each task's current total. Each task's last session is grown
/// forward by its share, so every positive share is always placed.
/// </summary>
internal static class DayDistribution
{
    /// <param name="daySessions">The day's sessions (as the week view groups them).</param>
    /// <param name="missing">The day's missing time to hand out.</param>
    public static DistributionPlan Plan(
        IReadOnlyList<TrackedSession> daySessions,
        TimeSpan missing)
    {
        if (missing <= TimeSpan.Zero || daySessions.Count == 0)
        {
            return DistributionPlan.Empty;
        }

        var tasks = GroupTasks(daySessions);
        var total = tasks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Total);
        if (total <= TimeSpan.Zero)
        {
            return DistributionPlan.Empty;
        }

        var shares = new List<AssignedShare>();
        var allocated = TimeSpan.Zero;
        for (var i = 0; i < tasks.Count; i++)
        {
            // Every share but the last is rounded to whole minutes; the last takes
            // the remainder, so the shares always add up to the missing time.
            var share = i == tasks.Count - 1
                ? missing - allocated
                : RoundToMinutes(missing * (tasks[i].Total.TotalSeconds / total.TotalSeconds));
            allocated += share;
            shares.Add(new AssignedShare(tasks[i].Name, share));
        }

        return Plan(shares, daySessions);
    }

    /// <summary>
    /// The proportional prefill of the distribute dialog: every share sits on the
    /// <paramref name="step"/> grid (floored, not rounded), and the last task takes
    /// the remainder, so the shares always add up to exactly <paramref name="missing"/>;
    /// the steppers adjust from there. Zero-share tasks are included with zero.
    /// </summary>
    /// <param name="daySessions">The day's sessions (as the week view groups them).</param>
    /// <param name="missing">The day's missing time to hand out.</param>
    /// <param name="step">The grid to place every share on, e.g. 15 minutes.</param>
    public static IReadOnlyList<AssignedShare> ProportionalShares(
        IReadOnlyList<TrackedSession> daySessions, TimeSpan missing, TimeSpan step)
    {
        if (missing <= TimeSpan.Zero || daySessions.Count == 0 || step <= TimeSpan.Zero)
        {
            return [];
        }

        var tasks = GroupTasks(daySessions);
        var total = tasks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Total);
        if (total <= TimeSpan.Zero)
        {
            return [];
        }

        var allocated = TimeSpan.Zero;
        var shares = new List<AssignedShare>();
        for (var i = 0; i < tasks.Count; i++)
        {
            var share = i == tasks.Count - 1
                ? missing - allocated
                : TimeSpan.FromTicks(step.Ticks * (long)Math.Floor(missing.TotalSeconds * tasks[i].Total.TotalSeconds / total.TotalSeconds / step.TotalSeconds));
            allocated += share;
            shares.Add(new AssignedShare(tasks[i].Name, share));
        }

        var result = shares;
        return result;
    }

    /// <summary>
    /// Plans handing each task of <paramref name="daySessions"/> exactly its assigned
    /// share out, growing the task's last session forward by its share.
    /// </summary>
    /// <param name="shares">The share per task, matched case-insensitively by name.</param>
    /// <param name="daySessions">The day's sessions to extend; the shares belong to them.</param>
    public static DistributionPlan Plan(
        IReadOnlyList<AssignedShare> shares,
        IReadOnlyList<TrackedSession> daySessions)
    {
        if (shares.Count == 0)
        {
            return DistributionPlan.Empty;
        }

        var tasks = GroupTasks(daySessions);
        var distributed = new List<DistributedTask>();

        foreach (var task in tasks)
        {
            var share = ShareOf(shares, task.Name);
            if (share <= TimeSpan.Zero)
            {
                continue;
            }

            var changes = Extend(task.Sessions[^1], share);
            distributed.Add(new DistributedTask(task.Name, task.Total, share, changes));
        }

        var result = new DistributionPlan(distributed);
        return result;
    }

    /// <summary>A day's tracked tasks in plan order: the grouped name, total and ordered sessions.</summary>
    internal static IReadOnlyList<DayTask> GroupTasks(IReadOnlyList<TrackedSession> daySessions) =>
    [
        .. daySessions
            .GroupBy(s => s.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new DayTask(
                g.Key,
                g.Aggregate(TimeSpan.Zero, (sum, s) => sum + Length(s)),
                (IReadOnlyList<TrackedSession>)[.. g.OrderBy(s => s.End)]))
            .Where(t => t.Total > TimeSpan.Zero)
            .OrderBy(t => t.Sessions.Min(s => s.Start))
    ];

    /// <summary>The share assigned to a task's name, or zero when none was given.</summary>
    private static TimeSpan ShareOf(IReadOnlyList<AssignedShare> shares, string taskName)
    {
        var match = shares.FirstOrDefault(s => s.Task.Equals(taskName, StringComparison.CurrentCultureIgnoreCase));
        var result = match?.Share ?? TimeSpan.Zero;
        return result;
    }

    /// <summary>Grows the task's last session forward by <paramref name="delta"/>.</summary>
    private static IReadOnlyList<SessionChange> Extend(TrackedSession last, TimeSpan delta)
    {
        var updated = last.Reschedule(last.Start, last.End + delta);
        return [new SessionChange(last, updated)];
    }

    /// <summary>Rounds a share to whole minutes so the planned times read cleanly.</summary>
    private static TimeSpan RoundToMinutes(TimeSpan duration) =>
        TimeSpan.FromMinutes(Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));

    private static TimeSpan Length(TrackedSession session) =>
        session.End > session.Start ? session.End - session.Start : TimeSpan.Zero;
}
