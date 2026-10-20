using Avalonia.Controls;
using Timetracker.Plugins.Calendar.Localization;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Calendar.Views;

namespace Timetracker.Plugins.Calendar;

/// <summary>
/// Contributes the calendar as a tab between the tracker and the options tab.
/// The shell places the tab and passes the add-in controls targeted at it.
/// </summary>
public sealed class CalendarTabContributor(CalendarViewModel calendar) : ITabQuery
{
    private readonly CalendarViewModel _calendar = calendar;

    public string TabKey => TabKeys.Calendar;

    public string Header => Strings.Tab_Calendar;

    public int Order => 1;

    public Control CreateView(IReadOnlyList<Control> contributors) => new CalendarTabView(_calendar, contributors);
}
