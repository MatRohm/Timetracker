namespace Timetracker.ActivityMonitor;

/// <summary>
/// Abstraction over the activity monitor's autostart installation per operating
/// system. Windows uses the per-user HKCU Run key; Linux uses a freedesktop
/// autostart <c>.desktop</c> file. Tests use an in-memory fake instead.
/// </summary>
public interface IActivityMonitorInstaller
{
    /// <summary>Full path of the monitor executable next to the main app.</summary>
    string MonitorExePath { get; }

    /// <summary>True when the autostart entry exists and points at this exe.</summary>
    bool IsInstalled { get; }

    /// <summary>Registers the autostart entry. Returns false when it failed.</summary>
    bool Install();

    /// <summary>Removes the autostart entry. Returns false when it failed.</summary>
    bool Uninstall();
}

/// <summary>Selects the autostart installer for the current operating system.</summary>
public static class ActivityMonitorInstallerFactory
{
    /// <summary>The installer matching the current platform.</summary>
    public static IActivityMonitorInstaller CreateForCurrentPlatform() =>
        OperatingSystem.IsWindows()
            ? new WindowsActivityMonitorInstaller()
            : new LinuxActivityMonitorInstaller();
}
