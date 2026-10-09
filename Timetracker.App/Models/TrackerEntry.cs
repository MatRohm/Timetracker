namespace Timetracker.App.Models;

public sealed class TrackerEntry
{
    /// <summary>Stable identity of the session, preserved across load and save.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Task { get; set; } = "";

    /// <summary>Stable identity of the task this session belongs to; empty when unknown.</summary>
    public Guid TaskId { get; set; }

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
        Id = Id,
        Task = Task,
        TaskId = TaskId,
        BookingElement = BookingElement,
        Start = Start,
        End = End,
        Duration = Duration,
        DurationSeconds = DurationSeconds,
    };

    /// <summary>The task's most recent non-empty booking element; empty when it has none.</summary>
    public static string LatestBookingElement(IEnumerable<TrackerEntry> entries, string task) =>
        entries
            .Where(e => e.Task.Trim().Equals(task.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(e => e.BookingElement)
            .LastOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";

    /// <summary>The task's stable id, reusing the first existing one or creating a new one.</summary>
    public static Guid ResolveTaskId(IEnumerable<TrackerEntry> entries, string task)
    {
        var existing = entries
            .Where(e => e.Task.Trim().Equals(task.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(e => e.TaskId)
            .FirstOrDefault(id => id != Guid.Empty);
        var result = OrNew(existing);
        return result;
    }

    /// <summary>A non-empty id, reusing <paramref name="id"/> or creating a new one.</summary>
    public static Guid OrNew(Guid id) => id != Guid.Empty ? id : Guid.NewGuid();
}
