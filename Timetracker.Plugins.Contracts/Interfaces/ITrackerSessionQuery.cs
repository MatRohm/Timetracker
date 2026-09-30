namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Reads the tracker's running-session state, so add-ins like the idle detector
/// can decide whether to act. Implemented by the main app's tracker view model;
/// add-in components resolve it via the DI container.
/// </summary>
public interface ITrackerSessionQuery
{
    /// <summary>True while a session is being tracked.</summary>
    bool IsSessionRunning { get; }
}
