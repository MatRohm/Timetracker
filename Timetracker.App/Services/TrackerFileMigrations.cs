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

    public string Read(string text)
    {
        using var json = JsonDocument.Parse(text);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("A version 1 tracker file must be a JSON array.");
        }

        var entries = VersionOneFileFormat.Read(json.RootElement);
        var legacy = LegacyTrackerFileFormat.ToDocument(entries, TargetVersion);
        return JsonSerializer.Serialize(legacy, TrackerFileFormat.JsonOptions);
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

/// <summary>The version-2/version-3 file shape: sessions still carry start, end and duration.</summary>
internal sealed class LegacyTrackerDocument
{
    public int Version { get; set; }

    public List<LegacyTrackedTask> Tasks { get; set; } = [];
}

internal sealed class LegacyTrackedTask
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string BookingElement { get; set; } = "";

    public List<LegacySessionSpan> Sessions { get; set; } = [];
}

internal sealed class LegacySessionSpan
{
    public Guid Id { get; set; }

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public string Duration { get; set; } = "";

    public double DurationSeconds { get; set; }
}

/// <summary>Groups entries into the legacy version-2 shape, mirroring <see cref="TrackerFileFormat"/>.</summary>
internal static class LegacyTrackerFileFormat
{
    public static LegacyTrackerDocument ToDocument(IEnumerable<TrackerEntry> entries, int version)
    {
        var tasks = entries
            .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new LegacyTrackedTask
            {
                Name = group.Key,
                BookingElement = group.Select(e => e.BookingElement)
                    .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "",
                Sessions = [.. group
                    .OrderBy(e => e.Start)
                    .Select(e => new LegacySessionSpan
                    {
                        Id = e.Id,
                        Start = e.Start,
                        End = e.End,
                        Duration = e.Duration,
                        DurationSeconds = e.DurationSeconds,
                    })],
            })
            .ToList();

        return new LegacyTrackerDocument { Version = version, Tasks = tasks };
    }
}
