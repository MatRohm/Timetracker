using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>A task whose day total is rounded: its old total, the target and the session changes.</summary>
public sealed record RoundedTask(string Name, TimeSpan Total, TimeSpan Target, IReadOnlyList<SessionChange> Changes);

/// <summary>A task that could not be rounded because no free time borders its last session.</summary>
public sealed record SkippedTask(string Name, TimeSpan Total);

/// <summary>What rounding a day would do: the rounded tasks and the tasks that had to be skipped.</summary>
public sealed record RoundingPlan(IReadOnlyList<RoundedTask> Tasks, IReadOnlyList<SkippedTask> Skipped)
{
    public static readonly RoundingPlan Empty = new([], []);

    /// <summary>Every session change of the rounded tasks, in task order.</summary>
    public IReadOnlyList<SessionChange> Changes { get; } = [.. Tasks.SelectMany(t => t.Changes)];

    /// <summary>True when some task of the day is not on a half hour.</summary>
    public bool HasWork => Tasks.Count > 0 || Skipped.Count > 0;
}

/// <summary>
/// Plans rounding each task's total on a day to the nearest half hour, for booking
/// systems that work in half hours. Exactly a quarter past rounds up, and a total
/// under 15 minutes becomes 0:30, so nothing booked disappears. Rounding up moves
/// the end of the task's last session later, or its start earlier when the end
/// would collide; rounding down shortens the latest sessions, removing any that
/// shrink to nothing. Nothing ever overlaps another session or the running timer.
/// </summary>
internal static class HalfHourRounding
{
    private static readonly TimeSpan HalfHour = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Quarter = TimeSpan.FromMinutes(15);

    /// <param name="daySessions">The day's sessions (as the week view groups them).</param>
    /// <param name="allSessions">Every stored session, so a change never overlaps one.</param>
    /// <param name="running">The running timer's range, if any; it is never touched or overlapped.</param>
    public static RoundingPlan Plan(
        IReadOnlyList<TrackedSession> daySessions,
        IReadOnlyList<TrackedSession> allSessions,
        TimeRange? running)
    {
        // Current ranges of every session; updated as changes are planned, so one
        // task's extension is respected by the next task's.
        var occupied = new Dictionary<TrackedSession, TimeRange>(ReferenceEqualityComparer.Instance);
        foreach (var session in allSessions.Concat(daySessions))
        {
            occupied.TryAdd(session, new TimeRange(session.Start, session.End));
        }

        var rounded = new List<RoundedTask>();
        var skipped = new List<SkippedTask>();

        var tasks = daySessions
            .GroupBy(s => s.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Min(s => s.Start));
        foreach (var task in tasks)
        {
            var sessions = task.OrderBy(s => s.End).ToList();
            var total = Total(sessions);
            var target = Target(total);
            if (total <= TimeSpan.Zero || target == total)
            {
                continue;
            }

            var taskChanges = target > total
                ? Extend(sessions[^1], target - total, occupied, running)
                : Shorten(sessions, total - target);
            if (taskChanges.Count == 0)
            {
                skipped.Add(new SkippedTask(task.Key, total));
                continue;
            }

            foreach (var change in taskChanges)
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

            rounded.Add(new RoundedTask(task.Key, total, target, taskChanges));
        }

        var result = new RoundingPlan(rounded, skipped);
        return result;
    }

    /// <summary>The nearest half hour, ties rounded up; anything under 15 minutes becomes 0:30.</summary>
    private static TimeSpan Target(TimeSpan total)
    {
        if (total < Quarter)
        {
            return HalfHour;
        }

        var halfHours = (total + Quarter).Ticks / HalfHour.Ticks;
        var result = TimeSpan.FromTicks(halfHours * HalfHour.Ticks);
        return result;
    }

    /// <summary>Moves the last session's end later, or else its start earlier; none when both collide.</summary>
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

    /// <summary>Shortens the latest sessions, working backwards; a session shrinking to nothing is removed.</summary>
    private static IReadOnlyList<SessionChange> Shorten(IReadOnlyList<TrackedSession> sessions, TimeSpan excess)
    {
        var changes = new List<SessionChange>();
        foreach (var session in sessions.Reverse())
        {
            if (excess <= TimeSpan.Zero)
            {
                break;
            }

            var length = Length(session);
            if (length > excess)
            {
                changes.Add(new SessionChange(session, session.Reschedule(session.Start, session.End - excess)));
                excess = TimeSpan.Zero;
            }
            else
            {
                changes.Add(new SessionChange(session, null));
                excess -= length;
            }
        }

        return changes;
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

    private static TimeSpan Total(IEnumerable<TrackedSession> sessions) =>
        sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + Length(s));

    private static TimeSpan Length(TrackedSession session) =>
        session.End > session.Start ? session.End - session.Start : TimeSpan.Zero;
}
