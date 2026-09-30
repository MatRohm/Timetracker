using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

/// <summary>
/// Plans how an untracked gap is handed to the sessions bordering it: the session
/// ending exactly where the gap starts and the one starting exactly where it ends.
/// Two neighbors share the gap by their durations (the longer session gets the
/// bigger slice) and meet at the split point; a single neighbor takes it all.
/// Sessions further away are never stretched, since that would also cover the
/// idle time in between.
/// </summary>
internal static class GapDistribution
{
    /// <summary>
    /// The changes that stretch the gap's neighbors over it; empty when no session
    /// touches the gap. The earlier neighbor's slice is whole minutes, the later one
    /// takes the remainder, so the slices always add up to the gap.
    /// </summary>
    public static IReadOnlyList<SessionChange> Plan(TimeRange gap, IEnumerable<TrackerEntry> sessions)
    {
        var candidates = sessions.ToList();
        var before = candidates.FirstOrDefault(s => s.End == gap.Start);
        var after = candidates.FirstOrDefault(s => s.Start == gap.End);

        if (before is null && after is null)
        {
            return [];
        }

        // A single neighbor takes the whole gap; two meet at the proportional split.
        var point = before is null ? gap.Start
            : after is null ? gap.End
            : gap.Start + BeforeSlice(gap.Duration, Length(before), Length(after));

        var changes = new List<SessionChange>();
        if (before is not null)
        {
            changes.Add(new SessionChange(before, Rescheduled(before, before.Start, point)));
        }
        if (after is not null)
        {
            changes.Add(new SessionChange(after, Rescheduled(after, point, after.End)));
        }

        return changes;
    }

    /// <summary>The earlier neighbor's share of the gap, rounded to whole minutes.</summary>
    private static TimeSpan BeforeSlice(TimeSpan gap, TimeSpan before, TimeSpan after)
    {
        var total = before + after;
        var share = total > TimeSpan.Zero ? before / total : 0.5;
        var minutes = Math.Round(gap.TotalMinutes * share, MidpointRounding.AwayFromZero);
        var slice = TimeSpan.FromMinutes(minutes);
        var result = slice > gap ? gap : slice;
        return result;
    }

    private static TimeSpan Length(TrackerEntry session) =>
        session.End > session.Start ? session.End - session.Start : TimeSpan.Zero;

    private static TrackerEntry Rescheduled(TrackerEntry session, DateTimeOffset start, DateTimeOffset end)
    {
        var result = session.Clone();
        result.Reschedule(start, end);
        return result;
    }
}
