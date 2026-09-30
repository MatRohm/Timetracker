namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// Severity of a status message shown in the week view's shared status line.
/// Mirrors the contract's <c>WeekStatusKind</c>; kept in the view-models namespace
/// so the week view model and its view express status without referencing Avalonia.
/// </summary>
public enum WeekStatus
{
    Info,
    Success,
    Error,
}
