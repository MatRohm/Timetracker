using System.Text.Json;
using Timetracker.App.Interfaces;

namespace Timetracker.App.Services;

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

    public string Read(string text)
    {
        var legacy = JsonSerializer.Deserialize<LegacyTrackerDocument>(text, TrackerFileFormat.JsonOptions)
            ?? throw new JsonException("A version 2 tracker file is empty.");
        foreach (var task in legacy.Tasks)
        {
            foreach (var session in task.Sessions)
            {
                if (session.Id == Guid.Empty)
                {
                    session.Id = Guid.NewGuid();
                }
            }
        }

        legacy.Version = TargetVersion;
        return JsonSerializer.Serialize(legacy, TrackerFileFormat.JsonOptions);
    }
}
