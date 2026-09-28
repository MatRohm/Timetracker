using Timetracker.Plugins;
using Timetracker.Plugins.Interfaces;
using Timetracker.ViewModels;

namespace Timetracker.Services;

/// <summary>
/// Host implementation for the week view: forwards an add-in's status messages to
/// the week view model's shared status line. Resolved by add-ins through the DI
/// container; the view binds to the view model's status rather than implementing
/// the host interface itself.
/// </summary>
public sealed class WeekStatusHost : IWeekStatusHost
{
    private readonly WeekViewModel _week;

    public WeekStatusHost(TrackerViewModel viewModel)
    {
        _week = viewModel.Week;
    }

    public void ShowStatus(string message, WeekStatusKind kind) =>
        _week.ShowStatus(message, Map(kind));

    private static WeekStatus Map(WeekStatusKind kind) => kind switch
    {
        WeekStatusKind.Success => WeekStatus.Success,
        WeekStatusKind.Error => WeekStatus.Error,
        _ => WeekStatus.Info,
    };
}
