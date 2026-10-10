namespace Timetracker.App.Interfaces;

/// <summary>Upgrades a tracker file to the current version by chaining migrations.</summary>
public interface ITrackerFileMigrationRunner
{
    /// <summary>
    /// Rewrites the file when it is older than the current version. Returns true
    /// when the file was migrated. Safe to call when no file exists.
    /// </summary>
    bool MigrateIfNeeded();
}
