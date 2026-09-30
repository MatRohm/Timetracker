using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>A task whose day total grows by its proportional share of the day's missing time.</summary>
public sealed record DistributedTask(string Name, TimeSpan Total, TimeSpan Share, IReadOnlyList<SessionChange> Changes);

/// <summary>What distributing a day's missing time over its tasks would do.</summary>
public sealed record DistributionPlan(IReadOnlyList<DistributedTask> Tasks, IReadOnlyList<SkippedTask> Skipped)
{
    public static readonly DistributionPlan Empty = new([], []);

    /// <summary>Every session change of the distributed tasks, in task order.</summary>
    public IReadOnlyList<SessionChange> Changes { get; } = [.. Tasks.SelectMany(t => t.Changes)];

    /// <summary>True when some task of the day can take a share or had to be skipped.</summary>
    public bool HasWork => Tasks.Count > 0 || Skipped.Count > 0;
}

/// <summary>
/// Plans distributing a day's missing (untracked) time over its tasks, in
/// proportion to each task's current total. Each task's last session is grown by
/// its share — later when free, else earlier — so a task whose last session is
/// boxed in on both sides is skipped. Nothing ever overlaps another session or
/// the running timer.
/// </summary>
internal static class DayDistribution
{
    /// <param name="daySessions">The day's sessions (as the week view groups them).</param>
    /// <param name="missing">The day's missing time to hand out.</param>
    /// <param name="allSessions">Every stored session, so a change never overlaps one.</param>
    /// <param name="running">The running timer's range, if any; never touched or overlapped.</param>
    public static DistributionPlan Plan(
        IReadOnlyList<TrackedSession> daySessions,
        TimeSpan missing,
        IReadOnlyList<TrackedSession> allSessions,
        TimeRange? running)
    {
        if (missing <= TimeSpan.Zero || daySessions.Count == 0)
        {
            return DistributionPlan.Empty;
        }

        // Current ranges of every session; updated as changes are planned, so one
        // task's extension is respected by the next task's.
        var occupied = new Dictionary<TrackedSession, TimeRange>(ReferenceEqualityComparer.Instance);
        foreach (var session in allSessions.Concat(daySessions))
        {
            occupied.TryAdd(session, new TimeRange(session.Start, session.End));
        }

        var tasks = daySessions
            .GroupBy(s => s.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new
            {
                Name = g.Key,
                Total = g.Aggregate(TimeSpan.Zero, (sum, s) => sum + Length(s)),
                Sessions = g.OrderBy(s => s.End).ToList(),
            })
            .Where(t => t.Total > TimeSpan.Zero)
            .OrderBy(t => t.Sessions.Min(s => s.Start))
            .ToList();

        var total = tasks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Total);
        if (total <= TimeSpan.Zero)
        {
            return DistributionPlan.Empty;
        }

        var distributed = new List<DistributedTask>();
        var skipped = new List<SkippedTask>();
        var allocated = TimeSpan.Zero;

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            // Every share but the last is rounded to whole minutes; the last takes
            // the remainder, so the shares always add up to the missing time.
            var share = i == tasks.Count - 1
                ? missing - allocated
                : RoundToMinutes(missing * (task.Total.TotalSeconds / total.TotalSeconds));
            if (share <= TimeSpan.Zero)
            {
                continue;
            }

            var changes = Extend(task.Sessions[^1], share, occupied, running);
            if (changes.Count == 0)
            {
                skipped.Add(new SkippedTask(task.Name, task.Total));
                continue;
            }

            foreach (var change in changes)
            {
                if (change.Updated is { } updated)
                {
                    occupied[change.Original] = new TimeRange(updated.Start, updated.End);
                }
                else
                {
                    occupied.Remove(change.Original);
                }
            }

            allocated += share;
            distributed.Add(new DistributedTask(task.Name, task.Total, share, changes));
        }

        var result = new DistributionPlan(distributed, skipped);
        return result;
    }

    /// <summary>Grows the task's last session by <paramref name="delta"/> later, or else earlier.</summary>
    private static IReadOnlyList<SessionChange> Extend(
        TrackedSession last,
        TimeSpan delta,
        Dictionary<TrackedSession, TimeRange> occupied,
        TimeRange? running)
    {
        var current = occupied[last];
        var later = new TimeRange(current.End, current.End + delta);
        if (IsFree(later, last, occupied, running))
        {
            return [new SessionChange(last, last.Reschedule(current.Start, later.End))];
        }

        var earlier = new TimeRange(current.Start - delta, current.Start);
        if (IsFree(earlier, last, occupied, running))
        {
            return [new SessionChange(last, last.Reschedule(earlier.Start, current.End))];
        }

        return [];
    }

    private static bool IsFree(
        TimeRange range, TrackedSession self, Dictionary<TrackedSession, TimeRange> occupied, TimeRange? running)
    {
        var others = occupied.Where(o => !ReferenceEquals(o.Key, self)).Select(o => o.Value);
        if (running is not null)
        {
            others = others.Append(running);
        }

        var result = !others.Any(o => o.Start < range.End && o.End > range.Start);
        return result;
    }

    /// <summary>Rounds a share to whole minutes so the planned times read cleanly.</summary>
    private static TimeSpan RoundToMinutes(TimeSpan duration) =>
        TimeSpan.FromMinutes(Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));

    private static TimeSpan Length(TrackedSession session) =>
        session.End > session.Start ? session.End - session.Start : TimeSpan.Zero;
}
