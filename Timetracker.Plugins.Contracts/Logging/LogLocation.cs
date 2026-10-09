namespace Timetracker.Plugins.Contracts.Logging;

/// <summary>
/// The folder every Timetracker process writes its log files to:
/// <c>~/.timetracker/logs</c>. The pre-unification platform-specific location is
/// preserved as <see cref="TimetrackerPaths.LegacyLogFolder"/> for the startup migration.
/// </summary>
public static class LogLocation
{
    /// <summary>The log folder of the current user on the current platform.</summary>
    public static string Directory => TimetrackerPaths.LogFolder;
}
