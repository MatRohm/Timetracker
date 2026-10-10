using System.Text.Json;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>
/// Migrates a version-3 file to version 4 by simplifying each session to a start
/// instant plus its length in seconds and giving every task a stable id. The
/// original file is copied to <c>&lt;file&gt;.v3-backup</c> before it is rewritten.
/// </summary>
public sealed class VersionThreeToFourMigration : ITrackerFileMigration
{
    public const int SourceVersion = 3;
    public const int TargetVersion = 4;
    public const string BackupExtension = ".v3-backup";

    private readonly string _jsonPath;

    public VersionThreeToFourMigration(string jsonPath)
    {
        _jsonPath = jsonPath;
    }

    public int FromVersion => SourceVersion;

    public int ToVersion => TargetVersion;

    public string BackupPath => _jsonPath + BackupExtension;

    public string Read(string text)
    {
        var legacy = JsonSerializer.Deserialize<LegacyTrackerDocument>(text, TrackerFileFormat.JsonOptions)
            ?? throw new JsonException("A version 3 tracker file is empty.");

        var document = new TrackerDocument
        {
            Version = TargetVersion,
            Tasks = [.. legacy.Tasks.Select(task => new TrackedTask
            {
                Id = TrackerEntry.OrNew(task.Id),
                Name = task.Name,
                BookingElement = task.BookingElement,
                Sessions = [.. task.Sessions.Select(session => new SessionSpan
                {
                    Id = session.Id,
                    DateStarted = session.Start,
                    DurationSeconds = TrackerFileFormat.SecondsBetween(session.Start, session.End),
                })],
            })],
        };

        return JsonSerializer.Serialize(document, TrackerFileFormat.JsonOptions);
    }
}
