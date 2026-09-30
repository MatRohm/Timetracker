using System.Text.Json;
using Timetracker.App.Models;
using Timetracker.App.Interfaces;

namespace Timetracker.App.Services;

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

        var document = TrackerFileFormat.ToDocument(VersionOneFileFormat.Read(json.RootElement));
        // The step emits version 2 (not the current version); the v2 -> v3 step follows.
        document.Version = TargetVersion;
        return document;
    }
}

/// <summary>
/// Migrates a version-2 file to version 3 by giving every session a stable
/// identity. The original file is copied to <c>&lt;file&gt;.v2-backup</c> before it
/// is rewritten; sessions that already carry an id are left untouched.
/// </summary>
public sealed class VersionTwoToThreeMigration : ITrackerFileMigration
{
    public const int SourceVersion = 2;
    public const int TargetVersion = 3;
    public const string BackupExtension = ".v2-backup";

    private readonly string _jsonPath;

    public VersionTwoToThreeMigration(string jsonPath)
    {
        _jsonPath = jsonPath;
    }

    public int FromVersion => SourceVersion;

    public int ToVersion => TargetVersion;

    public string BackupPath => _jsonPath + BackupExtension;

    public TrackerDocument Read(string text)
    {
        var document = JsonSerializer.Deserialize<TrackerDocument>(text, TrackerFileFormat.JsonOptions)
            ?? throw new JsonException("A version 2 tracker file is empty.");
        foreach (var task in document.Tasks)
        {
            foreach (var session in task.Sessions)
            {
                if (session.Id == Guid.Empty)
                {
                    session.Id = Guid.NewGuid();
                }
            }
        }

        document.Version = TargetVersion;
        return document;
    }
}
