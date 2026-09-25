using System.Text.Json;
using Timetracker.Models;

namespace Timetracker.Services;

/// <summary>
/// One step in upgrading a tracker file. Each step reads one version and produces
/// the next, so new versions are added by writing a new implementation instead of
/// changing existing ones (open/closed). <see cref="FromVersion"/>,
/// <see cref="ToVersion"/> and <see cref="BackupPath"/> are compile-time constants
/// on the implementation so a step always describes the range it handles.
/// </summary>
public interface ITrackerFileMigration
{
    /// <summary>Version this step reads.</summary>
    int FromVersion { get; }

    /// <summary>Version this step produces; equals <see cref="FromVersion"/> + 1.</summary>
    int ToVersion { get; }

    /// <summary>Copy of the original file kept before the step rewrites it.</summary>
    string BackupPath { get; }

    /// <summary>Reads a file of <see cref="FromVersion"/> and returns it at <see cref="ToVersion"/>.</summary>
    TrackerDocument Read(string text);
}

/// <summary>
/// Migrates the unversioned version-1 file (a flat array of sessions) to version 2
/// (one record per task with its sessions). The original file is copied to
/// <c>&lt;file&gt;.v1-backup</c> before it is rewritten.
/// </summary>
public sealed class VersionOneToTwoMigration : ITrackerFileMigration
{
    public const int SourceVersion = TrackerDocument.UnversionedVersion;
    public const int TargetVersion = 2;
    public const string BackupExtension = ".v1-backup";

    private readonly string _jsonPath;

    public VersionOneToTwoMigration(string jsonPath)
    {
        _jsonPath = jsonPath;
    }

    public int FromVersion => SourceVersion;

    public int ToVersion => TargetVersion;

    public string BackupPath => _jsonPath + BackupExtension;

    public TrackerDocument Read(string text)
    {
        using var json = JsonDocument.Parse(text);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("A version 1 tracker file must be a JSON array.");
        }

        return TrackerFileFormat.ToDocument(VersionOneFileFormat.Read(json.RootElement));
    }
}
