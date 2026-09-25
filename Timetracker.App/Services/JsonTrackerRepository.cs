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

/// <summary>Upgrades an older tracker file to the current format in place.</summary>
public interface ITrackerFileMigration
{
    /// <summary>
    /// Rewrites the file as version 2 when it still uses the unversioned format.
    /// Returns true when the file was migrated. Safe to call when no file exists.
    /// </summary>
    bool MigrateIfNeeded();
}

/// <summary>
/// JSON persistence for the time entries. The file carries a version marker
/// (<c>version: 2</c>) and stores one record per task with an array of the times
/// worked on it (see <see cref="TrackerFileFormat"/>). Files without a marker are
/// the version-1 flat array; they are read transparently and upgraded to version 2
/// either by <see cref="MigrateIfNeeded"/> at startup or by the next save.
/// </summary>
public sealed class JsonTrackerRepository : ITrackerRepository, ITrackerFileMigration
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

    public IReadOnlyList<TrackerEntry> GetAll() => LoadEntries();

    public void Add(TrackerEntry entry)
    {
        var entries = LoadEntries();
        entries.Add(entry);
        WriteAll(entries);
    }

    public void Save(IReadOnlyList<TrackerEntry> entries) => WriteAll([.. entries]);

    public bool MigrateIfNeeded()
    {
        if (!File.Exists(_jsonPath))
        {
            return false;
        }

        try
        {
            var text = File.ReadAllText(_jsonPath);
            if (string.IsNullOrWhiteSpace(text) || IsCurrentFormat(text))
            {
                return false;
            }

            // The file uses the unversioned (version 1) format: read it and rewrite
            // it as version 2, keeping the original if anything goes wrong.
            WriteAll(ReadEntries(text).ToList());
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A broken file must never stop startup; the normal load path reports
            // it and the next successful save keeps a backup.
            return false;
        }
    }

    private void WriteAll(List<TrackerEntry> entries)
    {
        var document = TrackerFileFormat.ToDocument(entries);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        WriteText(json);
    }

    /// <summary>Writes the file atomically, backing up a previously corrupt file first.</summary>
    private void WriteText(string json)
    {
        if (_fileWasCorrupt && File.Exists(_jsonPath))
        {
            // The existing file could not be parsed – keep a copy so nothing is ever lost.
            File.Copy(_jsonPath, _jsonPath + ".corrupt-backup", overwrite: true);
            _fileWasCorrupt = false;
        }

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

    private List<TrackerEntry> LoadEntries()
    {
        _fileWasCorrupt = false;
        try
        {
            if (!File.Exists(_jsonPath))
                return [];
            var text = File.ReadAllText(_jsonPath);
            if (string.IsNullOrWhiteSpace(text))
                return [];
            return [.. ReadEntries(text)];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _fileWasCorrupt = true;
            return [];
        }
    }

    /// <summary>
    /// Reads either format: a version-2 object, or the unversioned version-1 array
    /// (whose per-session entries may still use the legacy "description" name).
    /// </summary>
    private static IReadOnlyList<TrackerEntry> ReadEntries(string text)
    {
        using var json = JsonDocument.Parse(text);
        return json.RootElement.ValueKind switch
        {
            JsonValueKind.Array => ReadVersionOne(json),
            JsonValueKind.Object => ReadVersionTwo(text),
            _ => throw new JsonException("The tracker file is neither a version 1 array nor a version 2 object."),
        };
    }

    private static List<TrackerEntry> ReadVersionOne(JsonDocument json)
    {
        var entries = json.RootElement.Deserialize<List<LegacyEntryDto>>(JsonOptions) ?? [];
        return [.. entries.Select(ToEntry)];
    }

    private static List<TrackerEntry> ReadVersionTwo(string text)
    {
        var document = JsonSerializer.Deserialize<TrackerDocument>(text, JsonOptions)
            ?? throw new JsonException("The tracker file is empty.");
        if (document.Version != TrackerDocument.CurrentVersion)
        {
            throw new JsonException(
                $"Unsupported tracker file version {document.Version}; expected {TrackerDocument.CurrentVersion}.");
        }
        return TrackerFileFormat.ToEntries(document);
    }

    private static bool IsCurrentFormat(string text)
    {
        using var json = JsonDocument.Parse(text);
        return json.RootElement.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Maps a version-1 session. Older files stored the booking element under the
    /// JSON name "description"; that value is mapped to bookingElement so existing
    /// data survives the rename.
    /// </summary>
    private static TrackerEntry ToEntry(LegacyEntryDto dto) => new()
    {
        Task = dto.Task,
        BookingElement = dto.BookingElement.Length > 0 ? dto.BookingElement : dto.Description,
        Start = dto.Start,
        End = dto.End,
        Duration = dto.Duration,
        DurationSeconds = dto.DurationSeconds,
    };

    /// <summary>Load shape of a version-1 entry, tolerating both JSON field names.</summary>
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
