using System.Text.Json;
using Timetracker.Models;

namespace Timetracker.Services;

/// <summary>Upgrades a tracker file to the current version by chaining migrations.</summary>
public interface ITrackerFileMigrationRunner
{
    /// <summary>
    /// Rewrites the file when it is older than the current version. Returns true
    /// when the file was migrated. Safe to call when no file exists.
    /// </summary>
    bool MigrateIfNeeded();
}

/// <summary>
/// Upgrades a tracker file to the current version by applying migration steps in
/// version order: start at the file's stored version (files without a marker are
/// version <see cref="TrackerDocument.UnversionedVersion"/>) and chain the
/// registered steps until <see cref="TrackerDocument.CurrentVersion"/> is reached.
/// Adding a version means adding one <see cref="ITrackerFileMigration"/>; this
/// runner does not change. The original file is backed up before the first step
/// rewrites it, and a failure leaves the file untouched.
/// </summary>
public sealed class TrackerFileMigrator : ITrackerFileMigrationRunner
{
    private readonly string _jsonPath;
    private readonly IReadOnlyList<ITrackerFileMigration> _migrations;
    private readonly Action<string, Exception> _log;

    /// <param name="jsonPath">File to migrate.</param>
    /// <param name="migrations">Available steps (order does not matter).</param>
    /// <param name="log">Receives failures so startup can continue despite a bad file.</param>
    public TrackerFileMigrator(
        string jsonPath,
        IEnumerable<ITrackerFileMigration> migrations,
        Action<string, Exception>? log = null)
    {
        _jsonPath = jsonPath;
        _migrations = [.. migrations];
        _log = log ?? ((_, _) => { });
    }

    public bool MigrateIfNeeded()
    {
        if (!File.Exists(_jsonPath))
        {
            return false;
        }

        try
        {
            var text = File.ReadAllText(_jsonPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var version = ReadVersion(text);
            var chain = BuildChain(version, TrackerDocument.CurrentVersion, _migrations);
            if (chain.Count == 0)
            {
                return false;
            }

            File.Copy(_jsonPath, chain[0].BackupPath, overwrite: true);

            var document = ReadAsCurrent(text, chain);
            WriteDocument(document);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log("TrackerFileMigration", ex);
            return false;
        }
    }

    /// <summary>
    /// Reads the file's version. A file without a marker is an array and therefore
    /// <see cref="TrackerDocument.UnversionedVersion"/>; a marked file is an object.
    /// </summary>
    private static int ReadVersion(string text)
    {
        using var json = JsonDocument.Parse(text);
        return json.RootElement.ValueKind switch
        {
            JsonValueKind.Array => TrackerDocument.UnversionedVersion,
            JsonValueKind.Object => json.RootElement.TryGetProperty("version", out var version)
                ? version.GetInt32()
                : throw new JsonException(
                    $"A versioned tracker file must carry a \"version\" marker (expected {TrackerDocument.CurrentVersion})."),
            _ => throw new JsonException("The tracker file is neither an array nor an object."),
        };
    }

    /// <summary>
    /// The ordered steps from <paramref name="version"/> up to <paramref name="targetVersion"/>.
    /// Internal and pure so the chaining (including future multi-step upgrades) can
    /// be tested without files.
    /// </summary>
    internal static IReadOnlyList<ITrackerFileMigration> BuildChain(
        int version, int targetVersion, IReadOnlyList<ITrackerFileMigration> migrations)
    {
        var chain = new List<ITrackerFileMigration>();
        while (version < targetVersion)
        {
            var step = migrations.SingleOrDefault(m => m.FromVersion == version)
                ?? throw new JsonException($"No tracker file migration from version {version}.");
            chain.Add(step);
            version = step.ToVersion;
        }
        return chain;
    }

    /// <summary>Applies the steps in order, starting from the raw file text.</summary>
    private static TrackerDocument ReadAsCurrent(string text, IEnumerable<ITrackerFileMigration> chain)
    {
        TrackerDocument? document = null;
        foreach (var step in chain)
        {
            document = step.Read(document is null ? text : Serialize(document));
        }
        return document ?? throw new JsonException("The tracker file already matches the current version.");
    }

    private void WriteDocument(TrackerDocument document)
    {
        var json = JsonSerializer.Serialize(document, TrackerFileFormat.JsonOptions);
        var tempPath = _jsonPath + ".tmp";
        File.WriteAllText(tempPath, json);

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

    private static string Serialize(TrackerDocument document) =>
        JsonSerializer.Serialize(document, TrackerFileFormat.JsonOptions);
}
