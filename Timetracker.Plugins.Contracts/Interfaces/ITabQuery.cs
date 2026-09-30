using Avalonia.Controls;

namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Contributes a whole tab to the application window. The shell places the tab
/// between the tracker and the options tab (ordered by <see cref="Order"/>); the
/// contributor creates the tab's content and receives the add-in UI controls
/// targeted at its tab key.
/// </summary>
public interface ITabQuery
{
    /// <summary>Stable key of the tab, one of the <c>TabKeys</c> constants.</summary>
    string TabKey { get; }

    /// <summary>Header text shown on the tab.</summary>
    string Header { get; }

    /// <summary>Placement among the tabs; the tracker is first, the options tab last.</summary>
    int Order { get; }

    /// <summary>
    /// Creates the tab's content. <paramref name="contributors"/> are the pre-built
    /// add-in UI controls (from <see cref="IUiQuery"/>) targeted at this tab.
    /// </summary>
    Control CreateView(IReadOnlyList<Control> contributors);
}
