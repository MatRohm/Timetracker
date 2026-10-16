namespace Timetracker.Plugins.Contracts;

/// <summary>Tracking state of the tracker's session.</summary>
public enum TrackerState
{
    /// <summary>No session is being tracked.</summary>
    Stopped,

    /// <summary>A session is being tracked.</summary>
    Running,
}
