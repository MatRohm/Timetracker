using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// One session block in a day column. The instance is clipped to the column's
/// day, so a session crossing midnight becomes one block per day.
/// </summary>
public sealed class CalendarSessionViewModel(
    string task,
    string bookingElement,
    DateTimeOffset start,
    DateTimeOffset end,
    bool isRunning)
{
    /// <summary>Task name shown on the block.</summary>
    public string Task { get; } = task;

    /// <summary>Booking element shown in the tooltip; may be empty.</summary>
    public string BookingElement { get; } = bookingElement;

    /// <summary>Clipped start of the block.</summary>
    public DateTimeOffset Start { get; } = start;

    /// <summary>Clipped end of the block.</summary>
    public DateTimeOffset End { get; } = end;

    /// <summary>True for the session that is still running.</summary>
    public bool IsRunning { get; } = isRunning;

    /// <summary>Length of the clipped block.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Text the block's copy button copies.</summary>
    public string CopyText => Task;

    /// <summary>Clock range, e.g. "09:00–10:30".</summary>
    public string TimeText => CalendarTimeFormat.ClockRange(Start, End);

    /// <summary>Duration as "h:mm", e.g. "1:30".</summary>
    public string DurationText => CalendarTimeFormat.HoursMinutes(Duration);

    /// <summary>Tooltip: clock range, duration and the booking element when present.</summary>
    public string ToolTip
    {
        get
        {
            var result = $"{TimeText} · {DurationText}";
            if (BookingElement.Trim().Length > 0)
            {
                result = $"{result} · {BookingElement}";
            }

            return result;
        }
    }

    /// <summary>Creates the block from a stored session, treating an open end as now.</summary>
    public static CalendarSessionViewModel FromSession(
        TrackedSession session,
        DateTimeOffset now,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        bool isRunning)
    {
        var start = session.Start > dayStart ? session.Start : dayStart;
        var end = session.End > session.Start ? session.End : now;
        end = end < dayEnd ? end : dayEnd;

        var result = new CalendarSessionViewModel(
            session.Task,
            session.BookingElement,
            start,
            end,
            isRunning);
        return result;
    }
}
