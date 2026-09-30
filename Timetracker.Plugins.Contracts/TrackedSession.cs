namespace Timetracker.Plugins.Contracts;

/// <summary>
/// Read-only form of a stored session, exposed to add-ins. The app keeps its own
/// mutable entry type; instances here carry the same values but cannot be edited,
/// so an add-in can read and plan changes without reaching into the app's storage.
/// </summary>
public sealed record TrackedSession
{
    /// <summary>Stable identity of the session, so a planned change can be matched back to it.</summary>
    public Guid Id { get; init; }

    /// <summary>Task name this session was booked to.</summary>
    public string Task { get; init; } = "";

    /// <summary>Booking element this session belongs to; may be empty.</summary>
    public string BookingElement { get; init; } = "";

    public DateTimeOffset Start { get; init; }

    public DateTimeOffset End { get; init; }

    /// <summary>Duration as "hh:mm:ss".</summary>
    public string Duration { get; init; } = "";

    /// <summary>Duration in seconds.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// A copy with new start/end and a duration re-derived from them, so the
    /// duration can never drift from the times; a negative span is clamped to zero.
    /// </summary>
    public TrackedSession Reschedule(DateTimeOffset start, DateTimeOffset end)
    {
        var elapsed = end - start;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var result = this with
        {
            Start = start,
            End = end,
            Duration = elapsed.ToString(@"hh\:mm\:ss"),
            DurationSeconds = Math.Round(elapsed.TotalSeconds, 1),
        };
        return result;
    }
}
