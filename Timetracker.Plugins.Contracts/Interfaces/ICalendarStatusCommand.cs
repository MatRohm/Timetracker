namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Surface an add-in needs from the calendar: show one-line results in its
/// shared status line. Implemented by the main app's calendar.
/// </summary>
public interface ICalendarStatusCommand
{
    /// <summary>Shows a one-line status message; kind selects the color.</summary>
    void ShowStatus(string message, CalendarStatusKind kind);
}
