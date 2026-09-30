using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor;

/// <summary>
/// Feeds the week view from the activity log: the PC activity line per day (e.g.
/// "PC 7:15 active · 1:20 idle") and the day's active time, which the week view
/// compares with the booked time. Both come from the same per-day sum of the
/// log's active/idle spans; the active stretches themselves are exposed too, so
/// the week view can show when time went untracked.
/// </summary>
public sealed class ActivityWeekDayContributor : IWeekDayQuery, IDayActivityQuery
{
    /// <summary>Span kind the activity log uses for periods with user input.</summary>
    private const string ActiveKind = "active";

    private readonly ActivityLog _log;

    /// <summary>Spans of the last read, reused until the log file changes on disk.</summary>
    private IReadOnlyList<ActivitySpan>? _cachedSpans;

    /// <summary>Last-write time the cache was read from; a changed file invalidates it.</summary>
    private DateTime _cachedWriteTimeUtc;

    public ActivityWeekDayContributor(ActivityLog log)
    {
        _log = log;
    }

    public string GetDayText(DateOnly day)
    {
        var (activeSeconds, idleSeconds) = SumDay(day);
        if (activeSeconds <= 0 && idleSeconds <= 0)
        {
            return "";
        }

        var result = "PC " + HoursMinutes(activeSeconds) + " active";
        if (idleSeconds > 0)
        {
            result += " · " + HoursMinutes(idleSeconds) + " idle";
        }

        var text = result;
        return text;
    }

    public TimeSpan GetActiveTime(DateOnly day)
    {
        var (activeSeconds, _) = SumDay(day);
        var result = TimeSpan.FromSeconds(activeSeconds);
        return result;
    }

    public IReadOnlyList<ActiveSpan> GetActiveSpans(DateOnly day)
    {
        var result = DayParts(day)
            .Where(part => part.Kind == ActiveKind)
            .Select(part => new ActiveSpan(part.Start, part.End))
            .OrderBy(span => span.Start)
            .ToList();
        return result;
    }

    /// <summary>Sums the active and idle parts of the log that fall on <paramref name="day"/>.</summary>
    private (double ActiveSeconds, double IdleSeconds) SumDay(DateOnly day)
    {
        var activeSeconds = 0.0;
        var idleSeconds = 0.0;
        foreach (var part in DayParts(day))
        {
            var seconds = (part.End - part.Start).TotalSeconds;
            if (part.Kind == ActiveKind)
            {
                activeSeconds += seconds;
            }
            else
            {
                idleSeconds += seconds;
            }
        }

        var result = (activeSeconds, idleSeconds);
        return result;
    }

    /// <summary>
    /// Each logged span's part on <paramref name="day"/>: spans crossing midnight
    /// contribute only the part inside the day, spans on other days nothing.
    /// </summary>
    private IEnumerable<(string Kind, DateTimeOffset Start, DateTimeOffset End)> DayParts(DateOnly day)
    {
        foreach (var span in Spans())
        {
            // Clip the span to the day by comparing instants, so a span starting
            // mid-day counts from its start rather than from midnight.
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), span.Start.Offset);
            var dayEnd = dayStart.AddDays(1);
            var partStart = span.Start > dayStart ? span.Start : dayStart;
            var partEnd = span.End < dayEnd ? span.End : dayEnd;
            if (partEnd > partStart)
            {
                yield return (span.Kind, partStart, partEnd);
            }
        }
    }

    private static string HoursMinutes(double seconds) =>
        TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"h\:mm");

    /// <summary>
    /// The log's spans, read once and reused while the file is unchanged: the week
    /// view queries every day, so three calls per day would otherwise re-read and
    /// re-deserialize the whole file on every rebuild.
    /// </summary>
    private IReadOnlyList<ActivitySpan> Spans()
    {
        var writeTime = File.GetLastWriteTimeUtc(_log.FilePath);
        if (_cachedSpans is not null && writeTime == _cachedWriteTimeUtc)
        {
            return _cachedSpans;
        }

        _cachedSpans = _log.GetAll();
        _cachedWriteTimeUtc = writeTime;
        return _cachedSpans;
    }
}
