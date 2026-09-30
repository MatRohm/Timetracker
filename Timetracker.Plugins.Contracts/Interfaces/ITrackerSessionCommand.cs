namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Starts and stops the tracker's session, so the idle detector can live outside
/// the app. Implemented by the main app's tracker view model; add-in components
/// resolve it via the DI container.
/// </summary>
public interface ITrackerSessionCommand
{
    /// <summary>
    /// Starts a session for the task now; false when one is already running or
    /// the task name is empty.
    /// </summary>
    Task<bool> StartSessionAsync(string taskName, string bookingElement = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops and saves the running session at <paramref name="endedAt"/> (default:
    /// now); <paramref name="reason"/>, if given, is shown in the status line.
    /// False when none is running.
    /// </summary>
    Task<bool> StopSessionAsync(DateTimeOffset? endedAt = null, string? reason = null,
        CancellationToken cancellationToken = default);
}
