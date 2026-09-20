namespace Timetracker.AzureDevOps;

/// <summary>
/// Surface the Azure DevOps add-in needs from the main application. Implemented
/// by the tracker tab so the integration can fill the tracker inputs without
/// referencing the app project. Status feedback is shown by the add-in itself.
/// </summary>
public interface ITrackerUiHost
{
    /// <summary>Fills the task-name input (e.g. "1234 Fix login bug").</summary>
    void SetTaskName(string taskName);

    /// <summary>Fills the booking-element input (e.g. the "AZE-Element" field value).</summary>
    void SetBookingElement(string bookingElement);
}
