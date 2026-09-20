using System.Text.Json;
using System.Text.Json.Serialization;

namespace Timetracker.ActivityMonitor;

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

/// <summary>
/// Append-only JSON log of activity/idle spans at
/// <c>%USERPROFILE%\timetracker-activity.json</c>. Writes are atomic; the whole
/// file is rewritten on each append (the log stays small: at most two spans per
/// state change).
/// </summary>
public sealed class ActivityLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;

    /// <param name="filePath">Overrides the default path (used by tests).</param>
    public ActivityLog(string? filePath = null)
    {
        _path = filePath ?? DefaultFilePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "timetracker-activity.json");

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
