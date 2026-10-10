using System.Text.Json;
using Timetracker.Plugins.ActivityMonitor.Models;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// Append-only JSON log of activity/idle spans at
/// <c>~/.timetracker/timetracker-activity.json</c> (or the configured folder). Writes
/// are atomic; the whole file is rewritten on each append (the log stays small: at
/// most two spans per state change).
/// </summary>
/// <param name="filePath">Overrides the default path (used by tests).</param>
public sealed class ActivityLog(string? filePath = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path = filePath ?? DefaultFilePath;

    public static string DefaultFilePath => TimetrackerPaths.ActivityFile;

    public string FilePath => _path;

    /// <summary>Reads all recorded spans (oldest first); missing file yields empty.</summary>
    public IReadOnlyList<ActivitySpan> GetAll()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }
            var text = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }
            return JsonSerializer.Deserialize<List<ActivitySpan>>(text, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Appends one span, preserving all existing ones.</summary>
    public void Add(ActivitySpan span)
    {
        var spans = new List<ActivitySpan>(GetAll()) { span };
        var json = JsonSerializer.Serialize(spans, JsonOptions);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }

    /// <summary>Appends a span; convenience for tests and the monitor loop.</summary>
    public void Add(string kind, DateTimeOffset start, DateTimeOffset end) =>
        Add(new ActivitySpan(kind, start, end));
}
