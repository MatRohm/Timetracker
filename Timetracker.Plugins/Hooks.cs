namespace Timetracker.Plugins;

/// <summary>
/// A UI piece an add-in contributes to one of the application tabs. The shell
/// places the control and the add-in wires it up itself, using the services it
/// gets from the DI container.
/// </summary>
public interface IUiContributor
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
public interface IWeekDayContributor
{
    /// <summary>Text for the given day, e.g. "PC 7:15 active"; empty when none.</summary>
    string GetDayText(DateOnly day);
}

/// <summary>Runs once when the application starts or closes.</summary>
public interface IAppHook
{
    /// <summary>Called after the composition root built the container.</summary>
    void OnAppStarted(IServiceProvider services);

    /// <summary>Called when the main form is closing; the app exits afterwards.</summary>
    void OnAppClosing();
}
