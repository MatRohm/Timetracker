namespace Timetracker.ActivityMonitor;

/// <summary>
/// Abstraction over the activity monitor's autostart installation. The real
/// implementation reads/writes the per-user HKCU Run key; tests use an
/// in-memory fake so they never touch the Windows registry.
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
