namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Surface an add-in needs from the tracker tab: fill the inputs and show
/// status messages in the shared status line. Implemented by the main app's
/// tracker view; add-in components resolve it via the DI container.
/// </summary>
public interface ITrackerUiCommand
{
    /// <summary>Fills the task-name input (e.g. "1234 Fix login bug").</summary>
    void SetTaskName(string taskName);

    /// <summary>Fills the booking-element input (e.g. the "AZE-Element" field value).</summary>
    void SetBookingElement(string bookingElement);

    /// <summary>Shows a one-line status message in the tracker tab; kind selects the color.</summary>
    void ShowStatus(string message, TrackerStatusKind kind);
}

/// <summary>
/// Surface an add-in needs from the week view: show one-line results in its
/// shared status line. Implemented by the main app's week view.
/// </summary>
public interface IWeekStatusCommand
{
    /// <summary>Shows a one-line status message; kind selects the color.</summary>
    void ShowStatus(string message, WeekStatusKind kind);
}

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
