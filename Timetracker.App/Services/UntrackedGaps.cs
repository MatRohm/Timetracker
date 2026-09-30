using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>
/// Finds the untracked gaps of a day: the stretches in which the computer was
/// active but no tracked session (and no running timer) covers the time.
/// </summary>
internal static class UntrackedGaps
{
    /// <summary>
    /// Merges the active spans, subtracts every session and the running timer, and
    /// keeps the remaining stretches of at least <paramref name="minimum"/>, ordered
    /// by start.
    /// </summary>
    /// <param name="activeSpans">When the computer was used; may overlap or be unordered.</param>
    /// <param name="sessions">Tracked sessions; only their overlap with the active spans matters.</param>
    /// <param name="running">The running timer's range, if any; never reported as a gap.</param>
    /// <param name="minimum">Shorter gaps are dropped.</param>
    public static IReadOnlyList<TimeRange> Find(
        IEnumerable<TimeRange> activeSpans,
        IEnumerable<TrackerEntry> sessions,
        TimeRange? running,
        TimeSpan minimum)
    {
        var busy = sessions
            .Select(s => new TimeRange(s.Start, s.End))
            .Concat(running is null ? [] : [running])
            .Where(r => r.End > r.Start)
            .OrderBy(r => r.Start)
            .ToList();

        var gaps = new List<TimeRange>();
        foreach (var span in Merge(activeSpans))
        {
            var cursor = span.Start;
            foreach (var covered in busy.Where(b => b.End > span.Start && b.Start < span.End))
            {
                if (covered.Start > cursor)
                {
                    gaps.Add(new TimeRange(cursor, covered.Start));
                }

                if (covered.End > cursor)
                {
                    cursor = covered.End;
                }
            }

            if (cursor < span.End)
            {
                gaps.Add(new TimeRange(cursor, span.End));
            }
        }

        var result = gaps.Where(g => g.Duration >= minimum).ToList();
        return result;
    }

    /// <summary>Joins overlapping or touching spans into one, ordered by start.</summary>
    private static List<TimeRange> Merge(IEnumerable<TimeRange> spans)
    {
        var merged = new List<TimeRange>();
        foreach (var span in spans.Where(s => s.End > s.Start).OrderBy(s => s.Start))
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = last with { End = span.End > last.End ? span.End : last.End };
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }
}
