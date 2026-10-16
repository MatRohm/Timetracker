namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Reads the tracker's tracking state and its transitions, so add-ins can observe
/// when a session starts or stops — and the entry that does so — without polling.
/// Implemented by the main app; add-ins resolve it via the DI container.
/// </summary>
public interface ITrackerStateQuery
{
    /// <summary>The current tracking state.</summary>
    TrackerState State { get; }

    /// <summary>Raised when the tracking state changes, with the new state and its session.</summary>
    event Action<TrackerStateChangedEventArgs>? StateChanged;
}
