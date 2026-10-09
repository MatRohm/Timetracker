namespace Timetracker.Plugins.ActivityMonitor.Interfaces;

/// <summary>
/// Launches and gracefully stops the installed monitor process on demand. The
/// per-user autostart registration is handled separately by
/// <see cref="IActivityMonitorInstaller"/>; this controller manages only the
/// running process.
/// </summary>
public interface IActivityMonitorController
{
    /// <summary>Launches the monitor process detached; false when it could not start.</summary>
    bool Start();

    /// <summary>Asks the running monitor to stop gracefully; false on failure or timeout.</summary>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);
}
