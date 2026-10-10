namespace Timetracker.App.Interfaces;

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

    /// <summary>Reads a file of <see cref="FromVersion"/> and returns its text at <see cref="ToVersion"/>.</summary>
    string Read(string text);
}
