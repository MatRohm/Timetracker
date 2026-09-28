namespace Timetracker.App.ViewModels;

/// <summary>
/// Severity of a status message shown in the week view's shared status line. Mirrors
/// <see cref="TrackerStatus"/>; kept in the view-models namespace so the week view
/// model and its view can express status without referencing the plugin contract.
/// </summary>
public enum WeekStatus
{
    Info,
    Success,
    Error,
}
