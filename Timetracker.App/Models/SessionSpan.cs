namespace Timetracker.App.Models;

/// <summary>One allocated time span of a task: a start instant and its length in seconds.</summary>
public sealed class SessionSpan
{
    /// <summary>Stable identity of the session; empty for files written before it existed.</summary>
    public Guid Id { get; set; }

    public DateTimeOffset DateStarted { get; set; }

    public double DurationSeconds { get; set; }
}
