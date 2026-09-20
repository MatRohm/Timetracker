using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Contributes the PC activity line to the week view: sums the active/idle
/// spans of the log per day, shown as e.g. "PC 7:15 active · 1:20 idle".
/// </summary>
public sealed class ActivityWeekDayContributor : IWeekDayContributor
{
    private readonly ActivityLog _log;

    public ActivityWeekDayContributor(ActivityLog log)
    {
        _log = log;
    }

    public string GetDayText(DateOnly day)
    {
        var spans = _log.GetAll();
        var activeSeconds = 0.0;
        var idleSeconds = 0.0;
        foreach (var span in spans)
        {
            var spanStart = DateOnly.FromDateTime(span.Start.Date);
            var spanEnd = DateOnly.FromDateTime(span.End.Date);
            if (day < spanStart || day > spanEnd)
            {
                continue;
            }

            // Spans crossing midnight count with their per-day part.
            var overlapStart = span.Start.Date > day.ToDateTime(TimeOnly.MinValue)
                ? span.Start
                : new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), span.Start.Offset);
            var overlapEnd = span.End.Date < day.ToDateTime(TimeOnly.MinValue).AddDays(1)
                ? span.End
                : new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue).AddDays(1), span.End.Offset);
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

    private static string HoursMinutes(double seconds) =>
        TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"h\:mm");
}
