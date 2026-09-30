using System.Text.Json;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>
/// JSON persistence for the time entries. The file carries a version marker
/// (<c>version: 2</c>) and stores one record per task with an array of the times
/// worked on it (see <see cref="TrackerFileFormat"/>). Files without a marker are
/// the version-1 flat array; they are read transparently here and upgraded to the
/// current version by <see cref="TrackerFileMigrator"/> at startup.
/// </summary>
public sealed class JsonTrackerRepository : ITrackerRepository
{
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

    public async Task<IReadOnlyList<TrackerEntry>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await LoadEntriesAsync(cancellationToken);

    public async Task AddAsync(TrackerEntry entry, CancellationToken cancellationToken = default)
    {
        var entries = await LoadEntriesAsync(cancellationToken);
        entries.Add(entry);
        await WriteAllAsync(entries, cancellationToken);
    }

    public async Task SaveAsync(IReadOnlyList<TrackerEntry> entries, CancellationToken cancellationToken = default) =>
        await WriteAllAsync([.. entries], cancellationToken);

    private async Task WriteAllAsync(List<TrackerEntry> entries, CancellationToken cancellationToken)
    {
        var document = TrackerFileFormat.ToDocument(entries);
        var json = JsonSerializer.Serialize(document, TrackerFileFormat.JsonOptions);
        await WriteTextAsync(json, cancellationToken);
    }

    /// <summary>Writes the file atomically, backing up a previously corrupt file first.</summary>
    private async Task WriteTextAsync(string json, CancellationToken cancellationToken)
    {
        if (_fileWasCorrupt && File.Exists(_jsonPath))
        {
            // The existing file could not be parsed – keep a copy so nothing is ever lost.
            File.Copy(_jsonPath, _jsonPath + ".corrupt-backup", overwrite: true);
            _fileWasCorrupt = false;
        }

        // Atomic replace: readers never see a half-written file, and a failed write
        // leaves the previous file intact (the caller reports the error).
        await AtomicFile.WriteAllTextAsync(_jsonPath, json, cancellationToken);
    }

    private async Task<List<TrackerEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        _fileWasCorrupt = false;
        try
        {
            if (!File.Exists(_jsonPath))
                return [];
            var text = await File.ReadAllTextAsync(_jsonPath, cancellationToken);
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
    /// Reads either format: the current version-2 object, or the unversioned
    /// version-1 array, which is grouped into the current shape on the fly.
    /// </summary>
    private static IReadOnlyList<TrackerEntry> ReadEntries(string text)
    {
        using var json = JsonDocument.Parse(text);
        return json.RootElement.ValueKind switch
        {
            JsonValueKind.Array => VersionOneFileFormat.Read(json.RootElement),
            JsonValueKind.Object => ReadCurrent(text),
            _ => throw new JsonException("The tracker file is neither an array nor an object."),
        };
    }

    private static List<TrackerEntry> ReadCurrent(string text)
    {
        var document = JsonSerializer.Deserialize<TrackerDocument>(text, TrackerFileFormat.JsonOptions)
            ?? throw new JsonException("The tracker file is empty.");
        if (document.Version != TrackerDocument.CurrentVersion)
        {
            throw new JsonException(
                $"Unsupported tracker file version {document.Version}; expected {TrackerDocument.CurrentVersion}.");
        }
        return TrackerFileFormat.ToEntries(document);
    }
}
