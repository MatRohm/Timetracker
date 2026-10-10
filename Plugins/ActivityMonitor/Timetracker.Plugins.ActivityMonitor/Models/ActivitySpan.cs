using System.Text.Json.Serialization;

namespace Timetracker.Plugins.ActivityMonitor.Models;

/// <summary>
/// One activity span: the machine was active (user present), idle (no input) or
/// unknown (idle could not be measured) between <see cref="Start"/> and
/// <see cref="End"/>. Idle spans shorter than the configured threshold are never
/// written, so short breaks do not appear here.
/// </summary>
[method: JsonConstructor]
public sealed class ActivitySpan(string kind, DateTimeOffset start, DateTimeOffset end)
{
    /// <summary>"active", "idle" or "unknown".</summary>
    public string Kind { get; set; } = kind;

    public DateTimeOffset Start { get; set; } = start;

    public DateTimeOffset End { get; set; } = end;

    /// <summary>Duration text, e.g. "1:30".</summary>
    [JsonIgnore]
    public string DurationText => TimeSpan
        .FromSeconds(Math.Max(0, (End - Start).TotalSeconds))
        .ToString(@"h\:mm");
}
