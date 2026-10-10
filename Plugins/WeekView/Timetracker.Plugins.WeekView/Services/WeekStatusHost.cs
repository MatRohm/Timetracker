using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>
/// Host implementation for the week view's status line: forwards an add-in's
/// status message to the week view model. The week view owns its status line now
/// that it lives in this plugin.
/// </summary>
public sealed class WeekStatusHost(WeekViewModel week) : IWeekStatusCommand
{
    private readonly WeekViewModel _week = week;

    public void ShowStatus(string message, WeekStatusKind kind) =>
        _week.ShowStatus(message, Map(kind));

    private static WeekStatus Map(WeekStatusKind kind) => kind switch
    {
        WeekStatusKind.Success => WeekStatus.Success,
        WeekStatusKind.Error => WeekStatus.Error,
        _ => WeekStatus.Info,
    };
}
