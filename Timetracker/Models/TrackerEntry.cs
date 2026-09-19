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
