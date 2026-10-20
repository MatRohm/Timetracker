using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Services;

/// <summary>
/// Host implementation for the calendar's status line: forwards an add-in's
/// status message to the calendar model. The calendar owns its status line now
/// that it lives in this plugin.
/// </summary>
public sealed class CalendarStatusHost(CalendarViewModel calendar) : IWeekStatusCommand
{
    private readonly CalendarViewModel _calendar = calendar;

    public void ShowStatus(string message, WeekStatusKind kind) =>
        _calendar.ShowStatus(message, kind);
}
