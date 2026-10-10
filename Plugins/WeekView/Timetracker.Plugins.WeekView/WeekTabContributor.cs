using Avalonia.Controls;
using Timetracker.Plugins.WeekView.Localization;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.WeekView.ViewModels;
using Timetracker.Plugins.WeekView.Views;

namespace Timetracker.Plugins.WeekView;

/// <summary>
/// Contributes the week view as a tab between the tracker and the options tab.
/// The shell places the tab and passes the add-in controls targeted at it.
/// </summary>
public sealed class WeekTabContributor(WeekViewModel week) : ITabQuery
{
    private readonly WeekViewModel _week = week;

    public string TabKey => TabKeys.WeekView;

    public string Header => Strings.Tab_WeekView;

    public int Order => 1;

    public Control CreateView(IReadOnlyList<Control> contributors) => new WeekTabView(_week, contributors);
}
