using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>
/// Upgrades a tracker file to the current version by applying migration steps in
/// version order: start at the file's stored version (files without a marker are
/// version <see cref="TrackerDocument.UnversionedVersion"/>) and chain the
/// registered steps until <see cref="TrackerDocument.CurrentVersion"/> is reached.
/// Adding a version means adding one <see cref="ITrackerFileMigration"/>; this
/// runner does not change. The original file is backed up before the first step
/// rewrites it, and a failure leaves the file untouched.
/// </summary>
/// <param name="jsonPath">File to migrate.</param>
/// <param name="migrations">Available steps (order does not matter).</param>
/// <param name="logger">Receives failures so startup can continue despite a bad file.</param>
public sealed class TrackerFileMigrator(
    string jsonPath,
    IEnumerable<ITrackerFileMigration> migrations,
    ILogger<TrackerFileMigrator>? logger = null) : ITrackerFileMigrationRunner
{
    private readonly string _jsonPath = jsonPath;
    private readonly IReadOnlyList<ITrackerFileMigration> _migrations = [.. migrations];
    private readonly ILogger<TrackerFileMigrator> _logger = logger ?? NullLogger<TrackerFileMigrator>.Instance;

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

            var migrated = ReadAsCurrent(text, chain);
            WriteText(migrated);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not migrate the tracker file {Path}", _jsonPath);
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

    /// <summary>Applies the steps in order, piping the file text through each one.</summary>
    private static string ReadAsCurrent(string text, IEnumerable<ITrackerFileMigration> chain)
    {
        var current = text;
        foreach (var step in chain)
        {
            current = step.Read(current);
        }
        return current;
    }

    private void WriteText(string text)
    {
        // Atomic replace: readers never see a half-written file, and a failed write
        // leaves the original version in place (MigrateIfNeeded logs the error).
        AtomicFile.WriteAllText(_jsonPath, text);
    }
}
