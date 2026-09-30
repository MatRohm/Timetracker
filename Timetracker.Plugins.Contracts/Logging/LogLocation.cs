namespace Timetracker.Plugins.Contracts.Logging;

/// <summary>
/// The folder every Timetracker process writes its log files to, following each
/// platform's convention: <c>%LOCALAPPDATA%\Timetracker\logs</c> on Windows, and
/// <c>$XDG_STATE_HOME/timetracker/logs</c> (default <c>~/.local/state</c>) elsewhere,
/// where the XDG Base Directory specification puts logs.
/// </summary>
public static class LogLocation
{
    /// <summary>The log folder of the current user on the current platform.</summary>
    public static string Directory => Resolve(
        OperatingSystem.IsWindows(),
        Environment.GetEnvironmentVariable,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>
    /// Resolves the log folder from the given platform facts, so every platform can
    /// be tested on any machine.
    /// </summary>
    /// <param name="isWindows">True on Windows.</param>
    /// <param name="environment">Reads an environment variable; null when it is not set.</param>
    /// <param name="localAppData">The local application data folder (used on Windows).</param>
    /// <param name="home">The user's home folder (used elsewhere without XDG_STATE_HOME).</param>
    public static string Resolve(
        bool isWindows, Func<string, string?> environment, string localAppData, string home)
    {
        if (isWindows)
        {
            var windowsFolder = Path.Combine(localAppData, "Timetracker", "logs");
            return windowsFolder;
        }

        // The specification only allows absolute paths; anything else is ignored.
        var stateHome = environment("XDG_STATE_HOME");
        if (string.IsNullOrWhiteSpace(stateHome) || !Path.IsPathRooted(stateHome))
        {
            stateHome = Path.Combine(home, ".local", "state");
        }

        var result = Path.Combine(stateHome, "timetracker", "logs");
        return result;
    }
}
