using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Services;

/// <summary>
/// Host implementation for the calendar's status line: forwards an add-in's
/// status message to the calendar model. The calendar owns its status line now
/// that it lives in this plugin.
/// </summary>
public sealed class CalendarStatusHost(CalendarViewModel week) : IWeekStatusCommand
{
    private readonly CalendarViewModel _week = week;

    public void ShowStatus(string message, WeekStatusKind kind) =>
        _week.ShowStatus(message, Map(kind));

    private static CalendarStatus Map(WeekStatusKind kind) => kind switch
    {
        WeekStatusKind.Success => CalendarStatus.Success,
        WeekStatusKind.Error => CalendarStatus.Error,
        _ => CalendarStatus.Info,
    };
}
