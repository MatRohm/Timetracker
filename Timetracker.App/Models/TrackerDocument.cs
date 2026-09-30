namespace Timetracker.App.Models;

/// <summary>
/// The versioned, human-readable tracker file: one record per task with its name,
/// booking element and the sessions (allocated times) worked on it. Version 2 of
/// the format. Version 1 was a flat array of per-session entries with no marker;
/// the repository migrates it on startup.
/// </summary>
public sealed class TrackerDocument
{
    /// <summary>On-disk format this application writes.</summary>
    public const int CurrentVersion = 3;

    /// <summary>
    /// The implicit version of files written before the version marker existed: a
    /// flat array of per-session entries. <c>v1</c> is migrated up to
    /// <see cref="CurrentVersion"/> on load.
    /// </summary>
    public const int UnversionedVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<TrackedTask> Tasks { get; set; } = [];
}

/// <summary>One task in the file: its name, booking element and worked sessions.</summary>
public sealed class TrackedTask
{
    public string Name { get; set; } = "";

    public string BookingElement { get; set; } = "";

    public List<SessionSpan> Sessions { get; set; } = [];
}

/// <summary>One allocated time span of a task; the duration is kept alongside the times.</summary>
public sealed class SessionSpan
{
    /// <summary>Stable identity of the session; empty for files written before it existed.</summary>
    public Guid Id { get; set; }

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public string Duration { get; set; } = "";

    public double DurationSeconds { get; set; }
}
