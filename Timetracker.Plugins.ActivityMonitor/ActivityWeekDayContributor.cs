using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor;

/// <summary>
/// Feeds the week view from the activity log: the PC activity line per day (e.g.
/// "PC 7:15 active · 1:20 idle") and the day's active time, which the week view
/// compares with the booked time. Both come from the same per-day sum of the
/// log's active/idle spans.
/// </summary>
public sealed class ActivityWeekDayContributor : IWeekDayContributor, IDayActivitySource
{
    private readonly ActivityLog _log;

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

    /// <summary>
    /// Sums the active and idle spans of the log that fall on <paramref name="day"/>;
    /// spans crossing midnight count with their per-day part.
    /// </summary>
    private (double ActiveSeconds, double IdleSeconds) SumDay(DateOnly day)
    {
        var activeSeconds = 0.0;
        var idleSeconds = 0.0;
        foreach (var span in _log.GetAll())
        {
            // Clip the span to the day by comparing instants, so a span starting
            // mid-day counts from its start rather than from midnight.
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), span.Start.Offset);
            var dayEnd = dayStart.AddDays(1);
            var overlapStart = span.Start > dayStart ? span.Start : dayStart;
            var overlapEnd = span.End < dayEnd ? span.End : dayEnd;
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var seconds = (overlapEnd - overlapStart).TotalSeconds;
            if (span.Kind == "active")
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

    private static string HoursMinutes(double seconds) =>
        TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"h\:mm");
}
