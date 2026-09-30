using Avalonia.Controls;

namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// A UI piece an add-in contributes to one of the application tabs. The shell
/// places the control and the add-in wires it up itself, using the services it
/// gets from the DI container.
/// </summary>
public interface IUiQuery
{
    /// <summary>Stable key of the tab to place the control on ("Tracker", "Week view").</summary>
    string TargetTab { get; }

    /// <summary>
    /// Creates the control. Called once at startup; the shell only decides where
    /// to put it (button row / contributor band of the tab).
    /// </summary>
    Control CreateControl(IServiceProvider services);
}

/// <summary>
/// Contributes data lines shown per weekday in the week view (like the PC
/// activity line). Implement this to add further per-day summaries.
/// </summary>
public interface IWeekDayQuery
{
    /// <summary>Text for the given day, e.g. "PC 7:15 active"; empty when none.</summary>
    string GetDayText(DateOnly day);
}

/// <summary>
/// Supplies how long the computer was actively used on a day (e.g. from the PC
/// activity monitor). The week view compares it with the booked time to show how
/// much of the day was not tracked.
/// </summary>
public interface IDayActivityQuery
{
    /// <summary>Active time on the given day; <see cref="TimeSpan.Zero"/> when nothing was recorded.</summary>
    TimeSpan GetActiveTime(DateOnly day);

    /// <summary>
    /// The day's active stretches, clipped to the day and ordered by start; empty
    /// when nothing was recorded. Lets the week view show when time went untracked.
    /// </summary>
    IReadOnlyList<ActiveSpan> GetActiveSpans(DateOnly day);
}

/// <summary>Runs once when the application starts or closes.</summary>
public interface IAppCommand
{
    /// <summary>Called after the composition root built the container.</summary>
    void OnAppStarted(IServiceProvider services);

    /// <summary>Called when the main window is closing; the app exits afterwards.</summary>
    void OnAppClosing();
}
