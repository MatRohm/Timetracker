using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Services;

/// <summary>
/// Host implementation that lets an add-in start and stop the tracker's session:
/// forwards the request to the tracker view model, which drives its normal
/// start/stop path. The add-in resolves this through the DI container, so the
/// view model stays the only place that owns the running session.
/// </summary>
public sealed class TrackerSessionHost(TrackerViewModel viewModel) : ITrackerSessionQuery, ITrackerSessionCommand
{
    private readonly TrackerViewModel _viewModel = viewModel;

    public bool IsSessionRunning => _viewModel.IsRunning;

    public Task<bool> StartSessionAsync(string taskName, string bookingElement = "",
        CancellationToken cancellationToken = default) =>
        _viewModel.StartSessionAsync(taskName, bookingElement);

    public Task<bool> StopSessionAsync(DateTimeOffset? endedAt = null, string? reason = null,
        CancellationToken cancellationToken = default) =>
        _viewModel.StopSessionAsync(endedAt, reason);
}
