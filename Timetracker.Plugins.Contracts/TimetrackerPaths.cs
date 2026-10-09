namespace Timetracker.Plugins.Contracts;

/// <summary>
/// The single location of every config/data/log path. All artifacts default to
/// <c>~/.timetracker</c> on every OS; the legacy (pre-unification) locations are
/// kept as <c>Legacy*</c> members so the startup migration can move old files.
/// </summary>
public static class TimetrackerPaths
{
    /// <summary>The key of the option that relocates the data/log artifacts.</summary>
    public const string ConfigFolderKey = "General.ConfigFolder";

    /// <summary>The folder every artifact defaults to: <c>~/.timetracker</c>.</summary>
    public static string RootFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".timetracker");

    /// <summary>The options file — the fixed anchor, never relocated by <see cref="ConfigFolderKey"/>.</summary>
    public static string OptionsFile => Path.Combine(RootFolder, "timetracker-options.json");

    /// <summary>The tracked-sessions data file.</summary>
    public static string TrackingFile => Path.Combine(RootFolder, "timetracker.json");

    /// <summary>The activity log (active/idle spans).</summary>
    public static string ActivityFile => Path.Combine(RootFolder, "timetracker-activity.json");

    /// <summary>The monitor's last-state file.</summary>
    public static string ActivityStateFile => Path.Combine(RootFolder, "timetracker-activity.state.json");

    /// <summary>The folder every process writes its log files to.</summary>
    public static string LogFolder => Path.Combine(RootFolder, "logs");

    /// <summary>The effective folder for a data/log artifact: the configured folder, or the default.</summary>
    /// <param name="configuredFolder">The <see cref="ConfigFolderKey"/> option value, or null/empty.</param>
    public static string ResolveFolder(string? configuredFolder) =>
        string.IsNullOrWhiteSpace(configuredFolder) ? RootFolder : configuredFolder;

    // Legacy locations, one per artifact, so the migration can find files written by
    // the previous version. The options/tracking/activity/state files used to sit in
    // the user's home; the logs used to sit in a platform-specific state folder.

    /// <summary>The pre-unification options path.</summary>
    public static string LegacyOptionsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "timetracker-options.json");

    /// <summary>The pre-unification tracking path.</summary>
    public static string LegacyTrackingFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "timetracker.json");

    /// <summary>The pre-unification activity-log path.</summary>
    public static string LegacyActivityFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "timetracker-activity.json");

    /// <summary>The pre-unification monitor-state path.</summary>
    public static string LegacyActivityStateFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "timetracker-activity.state.json");

    /// <summary>The pre-unification log folder (<c>%LOCALAPPDATA%\Timetracker\logs</c> on Windows, XDG state elsewhere).</summary>
    public static string LegacyLogFolder => OperatingSystem.IsWindows()
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Timetracker", "logs")
        : Path.Combine(LegacyStateHome(), "timetracker", "logs");

    private static string LegacyStateHome()
    {
        // The XDG specification only allows absolute paths; anything else is ignored.
        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrWhiteSpace(stateHome) || !Path.IsPathRooted(stateHome))
        {
            stateHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        }

        return stateHome;
    }
}
