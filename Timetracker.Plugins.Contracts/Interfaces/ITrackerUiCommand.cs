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
