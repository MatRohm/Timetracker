using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Geometry of the Workweek/Week grid: fitting the time axis to the visible data,
/// mapping times to pixels and laying overlapping blocks out in lanes. Pure
/// functions so the layout can be tested without Avalonia.
/// </summary>
public static class CalendarLayout
{
    /// <summary>Vertical pixels one hour occupies.</summary>
    public const double PixelsPerHour = 48;

    /// <summary>Shortest window the axis shows, so a single short session still has context.</summary>
    public static readonly TimeSpan MinimumWindow = TimeSpan.FromHours(8);

    /// <summary>Granularity the axis bounds are snapped to.</summary>
    public static readonly TimeSpan Snap = TimeSpan.FromMinutes(30);

    private const double MinutesPerDay = 24 * 60;

    /// <summary>
    /// Fits the axis around <paramref name="ranges"/> and, when today is visible,
    /// <paramref name="now"/>: snaps outwards to half hours, keeps at least
    /// <see cref="MinimumWindow"/> and clamps to the day. Returns
    /// <see cref="CalendarAxis.Default"/> when there is nothing to fit.
    /// </summary>
    public static CalendarAxis FitAxis(IEnumerable<TimeRange> ranges, TimeSpan? now = null)
    {
        var minutes = new List<double>();
        foreach (var range in ranges)
        {
            minutes.Add(range.Start.TimeOfDay.TotalMinutes);
            minutes.Add(EndMinute(range));
        }

        if (now is { } moment)
        {
            minutes.Add(moment.TotalMinutes);
        }

        if (minutes.Count == 0)
        {
            return CalendarAxis.Default;
        }

        var start = FloorToSnap(minutes.Min());
        var end = CeilingToSnap(minutes.Max());
        (start, end) = EnsureMinimum(start, end);

        var result = new CalendarAxis(TimeSpan.FromMinutes(start), TimeSpan.FromMinutes(end));
        return result;
    }

    /// <summary>
    /// Assigns each block a lane so overlapping blocks sit side by side. Blocks
    /// that merely touch (one ends when the next starts) share a lane.
    /// </summary>
    public static IReadOnlyList<OverlapLane> AssignLanes(IReadOnlyList<TimeRange> blocks)
    {
        var result = new OverlapLane[blocks.Count];
        var order = Enumerable.Range(0, blocks.Count)
            .OrderBy(i => blocks[i].Start)
            .ThenBy(i => blocks[i].End)
            .ToArray();

        var index = 0;
        while (index < order.Length)
        {
            var clusterEnd = blocks[order[index]].End;
            var laneEnds = new List<DateTimeOffset>();
            var members = new List<(int Block, int Lane)>();

            var next = index;
            while (next < order.Length && blocks[order[next]].Start < clusterEnd)
            {
                var block = order[next];
                var lane = laneEnds.FindIndex(end => end <= blocks[block].Start);
                if (lane < 0)
                {
                    lane = laneEnds.Count;
                    laneEnds.Add(blocks[block].End);
                }
                else
                {
                    laneEnds[lane] = blocks[block].End;
                }

                members.Add((block, lane));
                if (blocks[block].End > clusterEnd)
                {
                    clusterEnd = blocks[block].End;
                }

                next++;
            }

            foreach (var (block, lane) in members)
            {
                result[block] = new OverlapLane(lane, laneEnds.Count);
            }

            index = next;
        }

        return result;
    }

    /// <summary>Vertical offset of <paramref name="time"/> in pixels, clipped to the axis.</summary>
    public static double Top(TimeSpan time, CalendarAxis axis)
    {
        var offset = (time - axis.Start).TotalHours * PixelsPerHour;
        var result = Math.Clamp(offset, 0, axis.Height);
        return result;
    }

    /// <summary>Height in pixels of a [start, end) stretch, clipped to the axis.</summary>
    public static double Height(TimeSpan start, TimeSpan end, CalendarAxis axis)
    {
        var top = Top(start, axis);
        var bottom = Top(end, axis);
        var result = Math.Max(1, bottom - top);
        return result;
    }

    /// <summary>
    /// Minute-of-day at which <paramref name="range"/> ends. A clipped range that
    /// ends exactly at midnight reports 24:00 rather than 00:00.
    /// </summary>
    private static double EndMinute(TimeRange range)
    {
        var end = range.End.TimeOfDay.TotalMinutes;
        if (end <= range.Start.TimeOfDay.TotalMinutes && range.End.Date > range.Start.Date)
        {
            end = MinutesPerDay;
        }

        return end;
    }

    private static double FloorToSnap(double minutes) =>
        Math.Floor(minutes / Snap.TotalMinutes) * Snap.TotalMinutes;

    private static double CeilingToSnap(double minutes) =>
        Math.Ceiling(minutes / Snap.TotalMinutes) * Snap.TotalMinutes;

    private static (double Start, double End) EnsureMinimum(double start, double end)
    {
        if (end - start >= MinimumWindow.TotalMinutes)
        {
            return (start, end);
        }

        // Centre the minimum window on the data, snapping the centre so both
        // bounds stay on half hours.
        var center = FloorToSnap((start + end) / 2);
        var half = MinimumWindow.TotalMinutes / 2;
        start = center - half;
        end = center + half;

        if (start < 0)
        {
            end -= start;
            start = 0;
        }

        if (end > MinutesPerDay)
        {
            start -= end - MinutesPerDay;
            end = MinutesPerDay;
        }

        return (start, end);
    }
}
