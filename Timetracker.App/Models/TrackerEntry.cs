namespace Timetracker.Models;

public sealed class TrackerEntry
{
    public string Task { get; set; } = "";

    /// <summary>Booking element this session belongs to; may be empty.</summary>
    public string BookingElement { get; set; } = "";

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string Duration { get; set; } = "";
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Moves the session to a new start/end and re-derives its duration. The
    /// duration is always computed from the times so it can never drift from them;
    /// a negative span is clamped to zero.
    /// </summary>
    public void Reschedule(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;

        var elapsed = end - start;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        Duration = elapsed.ToString(@"hh\:mm\:ss");
        DurationSeconds = Math.Round(elapsed.TotalSeconds, 1);
    }

    public TrackerEntry Clone() => new()
    {
        Task = Task,
        BookingElement = BookingElement,
        Start = Start,
        End = End,
        Duration = Duration,
        DurationSeconds = DurationSeconds,
    };
}
