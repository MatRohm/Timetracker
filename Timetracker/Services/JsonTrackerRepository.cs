using System.Text.Encodings.Web;
using System.Text.Json;
using Timetracker.Models;

namespace Timetracker.Services;

public interface ITrackerRepository
{
    /// <summary>Full path of the JSON file the entries are stored in.</summary>
    string FilePath { get; }

    /// <summary>Reads all entries.</summary>
    IReadOnlyList<TrackerEntry> GetAll();

    /// <summary>Appends one entry, preserving every existing one.</summary>
    void Add(TrackerEntry entry);

    /// <summary>Writes the given entries, replacing the stored list (used by edits and deletes).</summary>
    void Save(IReadOnlyList<TrackerEntry> entries);
}

public sealed class JsonTrackerRepository : ITrackerRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _jsonPath;

    private bool _fileWasCorrupt;

    /// <param name="filePath">Overrides the default path (used by tests); null = %USERPROFILE%\timetracker.json.</param>
    public JsonTrackerRepository(string? filePath = null)
    {
        _jsonPath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "timetracker.json");
    }

    public string FilePath => _jsonPath;

    public IReadOnlyList<TrackerEntry> GetAll() => LoadList();

    public void Add(TrackerEntry entry)
    {
        var entries = LoadList();
        entries.Add(entry);
        WriteAll(entries);
    }

    public void Save(IReadOnlyList<TrackerEntry> entries) => WriteAll([.. entries]);

    private void WriteAll(List<TrackerEntry> entries)
    {
        if (_fileWasCorrupt && File.Exists(_jsonPath))
        {
            // The existing file could not be parsed – keep a copy so nothing is ever lost.
            File.Copy(_jsonPath, _jsonPath + ".corrupt-backup", overwrite: true);
            _fileWasCorrupt = false;
        }

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        var tempPath = _jsonPath + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(_jsonPath))
        {
            try
            {
                // Atomic replace: readers never see a half-written file.
                File.Replace(tempPath, _jsonPath, destinationBackupFileName: null);
            }
            catch (IOException)
            {
                // Fall back to a direct write if the replace is not possible.
                File.WriteAllText(_jsonPath, json);
                File.Delete(tempPath);
            }
        }
        else
        {
            File.Move(tempPath, _jsonPath);
        }
    }

    private List<TrackerEntry> LoadList()
    {
        _fileWasCorrupt = false;
        try
        {
            if (!File.Exists(_jsonPath))
                return [];
            var text = File.ReadAllText(_jsonPath);
            if (string.IsNullOrWhiteSpace(text))
                return [];
            return MigrateLegacy(
                JsonSerializer.Deserialize<List<LegacyEntryDto>>(text, JsonOptions) ?? []);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _fileWasCorrupt = true;
            return [];
        }
    }

    /// <summary>
    /// Older files stored the booking element under the JSON name "description";
    /// map that value to <see cref="TrackerEntry.BookingElement"/> so existing
    /// data survives the rename. Saves always write the new name.
    /// </summary>
    private static List<TrackerEntry> MigrateLegacy(IEnumerable<LegacyEntryDto> dtos) =>
        [.. dtos.Select(d => new TrackerEntry
        {
            Task = d.Task,
            BookingElement = d.BookingElement.Length > 0 ? d.BookingElement : d.Description,
            Start = d.Start,
            End = d.End,
            Duration = d.Duration,
            DurationSeconds = d.DurationSeconds,
        })];

    /// <summary>Load shape that tolerates both the legacy and the current JSON name.</summary>
    private sealed class LegacyEntryDto
    {
        public string Task { get; set; } = "";
        public string Description { get; set; } = "";
        public string BookingElement { get; set; } = "";
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public string Duration { get; set; } = "";
        public double DurationSeconds { get; set; }
    }
}
