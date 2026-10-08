namespace Timetracker.Plugins.ActivityMonitor.Interfaces;

/// <summary>
/// Queries the running state of the activity monitor process. Returns
/// <see cref="MonitorStatus.Running"/> when the monitor answers,
/// <see cref="MonitorStatus.Stopped"/> when it does not, and
/// <see cref="MonitorStatus.Unknown"/> when the state cannot be determined. The
/// "uninstalled" state is not part of this query; the caller derives it from the
/// installer.
/// </summary>
public interface IActivityMonitorStatusQuery
{
    Task<MonitorStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
