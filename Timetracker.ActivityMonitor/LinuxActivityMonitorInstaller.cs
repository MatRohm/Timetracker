namespace Timetracker.ActivityMonitor;

/// <summary>
/// Linux autostart installer: writes a freedesktop <c>.desktop</c> entry to the
/// per-user autostart directory, which every major desktop environment reads at
/// logon. No root rights are needed and nothing outside the user's home is
/// touched. Mirrors <see cref="WindowsActivityMonitorInstaller"/>.
/// </summary>
public sealed class LinuxActivityMonitorInstaller : IActivityMonitorInstaller
{
    private const string DesktopFileName = "timetracker-activity-monitor.desktop";

    /// <summary>Full path of the monitor executable next to the main app.</summary>
    public string MonitorExePath => Path.Combine(
        AppContext.BaseDirectory, "Timetracker.ActivityMonitor");

    /// <summary>Path of the autostart .desktop entry.</summary>
    public string DesktopFilePath => Path.Combine(AutostartDirectory, DesktopFileName);

    private static string AutostartDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "autostart");

    /// <summary>True when the autostart entry exists and points at this exe.</summary>
    public bool IsInstalled
    {
        get
        {
            try
            {
                if (!File.Exists(DesktopFilePath))
                {
                    return false;
                }

                var content = File.ReadAllText(DesktopFilePath);
                var expected = $"Exec={MonitorExePath}";
                var result = content
                    .Split('\n')
                    .Any(line => line.Trim().Equals(expected, StringComparison.Ordinal));
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <summary>Writes the autostart entry. Returns false when it failed.</summary>
    public bool Install()
    {
        if (!File.Exists(MonitorExePath))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(AutostartDirectory);
            File.WriteAllText(DesktopFilePath, BuildDesktopEntry());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Removes the autostart entry. Returns false when it failed.</summary>
    public bool Uninstall()
    {
        try
        {
            if (!File.Exists(DesktopFilePath))
            {
                return true; // Nothing to remove counts as success.
            }
            File.Delete(DesktopFilePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string BuildDesktopEntry() =>
        "[Desktop Entry]\n" +
        "Type=Application\n" +
        $"Name=Timetracker activity monitor\n" +
        $"Exec={MonitorExePath}\n" +
        "X-GNOME-Autostart-enabled=true\n" +
        "Terminal=false\n" +
        "NoDisplay=true\n";
}
