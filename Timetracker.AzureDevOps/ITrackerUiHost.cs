namespace Timetracker.AzureDevOps;

/// <summary>
/// Surface the Azure DevOps add-in needs from the main application. Implemented
/// by the tracker tab so the integration can fill the tracker inputs and show
/// status messages in the shared status line without referencing the app
/// project.
/// </summary>
public interface ITrackerUiHost
{
    /// <summary>Fills the task-name input (e.g. "1234 Fix login bug").</summary>
    void SetTaskName(string taskName);

    /// <summary>Fills the booking-element input (e.g. the "AZE-Element" field value).</summary>
    void SetBookingElement(string bookingElement);

    /// <summary>Shows a one-line status message; kind selects the color.</summary>
    void ShowStatus(string message, TrackerStatusKind kind);
}

/// <summary>Severity of a status message shown by the host UI.</summary>
public enum TrackerStatusKind
{
    Info,
    Success,
    Error,
}
