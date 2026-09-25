using Timetracker.Models;

namespace Timetracker.Interfaces;

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

/// <summary>Upgrades a tracker file to the current version by chaining migrations.</summary>
public interface ITrackerFileMigrationRunner
{
    /// <summary>
    /// Rewrites the file when it is older than the current version. Returns true
    /// when the file was migrated. Safe to call when no file exists.
    /// </summary>
    bool MigrateIfNeeded();
}
