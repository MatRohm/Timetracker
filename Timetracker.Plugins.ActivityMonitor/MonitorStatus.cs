namespace Timetracker.Plugins.ActivityMonitor;

/// <summary>The four statuses the options view shows for the PC activity monitor.</summary>
public enum MonitorStatus
{
    /// <summary>The running state could not be determined.</summary>
    Unknown,

    /// <summary>No autostart entry exists for the monitor.</summary>
    Uninstalled,

    /// <summary>Installed, but the monitor process is not answering.</summary>
    Stopped,

    /// <summary>Installed and the monitor process answers the status query.</summary>
    Running,
}
