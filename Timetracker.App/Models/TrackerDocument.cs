namespace Timetracker.App.Models;

/// <summary>
/// The versioned, human-readable tracker file: one record per task with its name,
/// booking element and the sessions (allocated times) worked on it. Version 4 stores
/// each session as a start instant plus its length in seconds, and gives every task
/// a stable id like its sessions already carry. Older versions are migrated on load.
/// </summary>
public sealed class TrackerDocument
{
    /// <summary>On-disk format this application writes.</summary>
    public const int CurrentVersion = 4;

    /// <summary>
    /// The implicit version of files written before the version marker existed: a
    /// flat array of per-session entries. <c>v1</c> is migrated up to
    /// <see cref="CurrentVersion"/> on load.
    /// </summary>
    public const int UnversionedVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<TrackedTask> Tasks { get; set; } = [];
}
