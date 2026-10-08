using System.Text.Json.Serialization;

namespace Timetracker.Plugins.ActivityMonitor.Models;

/// <summary>
/// One activity span: the machine was active (user present) or idle (no input)
/// between <see cref="Start"/> and <see cref="End"/>. Idle spans shorter than the
/// configured threshold are never written, so short breaks do not appear here.
/// </summary>
public sealed class ActivitySpan
{
    [JsonConstructor]
    public ActivitySpan(string kind, DateTimeOffset start, DateTimeOffset end)
    {
        Kind = kind;
        Start = start;
        End = end;
    }

    /// <summary>"active" or "idle".</summary>
    public string Kind { get; set; }

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    /// <summary>Duration text, e.g. "1:30".</summary>
    [JsonIgnore]
    public string DurationText => TimeSpan
        .FromSeconds(Math.Max(0, (End - Start).TotalSeconds))
        .ToString(@"h\:mm");
}
