using Timetracker.ActivityMonitor.Interfaces;

namespace Timetracker.ActivityMonitor;

/// <summary>Selects the autostart installer for the current operating system.</summary>
public static class ActivityMonitorInstallerFactory
{
    /// <summary>The installer matching the current platform.</summary>
    public static IActivityMonitorInstaller CreateForCurrentPlatform() =>
        OperatingSystem.IsWindows()
            ? new WindowsActivityMonitorInstaller()
            : new LinuxActivityMonitorInstaller();
}
