namespace Timetracker.Models;

public sealed class TrackerEntry
{
    public string Task { get; set; } = "";

    /// <summary>Optional free-text note; may be empty.</summary>
    public string Description { get; set; } = "";

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string Duration { get; set; } = "";
    public double DurationSeconds { get; set; }
}