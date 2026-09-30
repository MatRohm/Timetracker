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
