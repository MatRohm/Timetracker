using System.Text.Json;
using Timetracker.Models;
using Timetracker.Interfaces;

namespace Timetracker.Services;

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
