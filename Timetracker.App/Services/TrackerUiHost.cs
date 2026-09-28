using Timetracker.Plugins;
using Timetracker.Plugins.Interfaces;
using Timetracker.ViewModels;

namespace Timetracker.Services;

/// <summary>
/// Host implementation for the tracker tab: forwards an add-in's requests to the
/// tracker view model, which drives the task input, booking-element preview and
/// status line through its normal bindings. Replaces the previous static
/// <c>UiHostAccessor</c> bridge, so the view no longer implements the host itself.
/// </summary>
public sealed class TrackerUiHost : ITrackerUiHost
{
    private readonly TrackerViewModel _viewModel;

    public TrackerUiHost(TrackerViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void SetTaskName(string taskName) => _viewModel.TaskName = taskName;

    public void SetBookingElement(string bookingElement) =>
        _viewModel.PreviewBookingElement = bookingElement;

    public void ShowStatus(string message, TrackerStatusKind kind) =>
        _viewModel.ShowStatus(message, Map(kind));

    private static TrackerStatus Map(TrackerStatusKind kind) => kind switch
    {
        TrackerStatusKind.Success => TrackerStatus.Success,
        TrackerStatusKind.Error => TrackerStatus.Error,
        _ => TrackerStatus.Info,
    };
}
