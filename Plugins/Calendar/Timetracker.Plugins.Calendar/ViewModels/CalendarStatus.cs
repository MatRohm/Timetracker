namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Severity of a status message shown in the calendar's shared status line.
/// Mirrors the contract's <c>WeekStatusKind</c>; kept in the view-models namespace
/// so the calendar model and its view express status without referencing Avalonia.
/// </summary>
public enum CalendarStatus
{
    Info,
    Success,
    Error,
}
