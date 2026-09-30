namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Surface an add-in needs from the week view: show one-line results in its
/// shared status line. Implemented by the main app's week view.
/// </summary>
public interface IWeekStatusCommand
{
    /// <summary>Shows a one-line status message; kind selects the color.</summary>
    void ShowStatus(string message, WeekStatusKind kind);
}
