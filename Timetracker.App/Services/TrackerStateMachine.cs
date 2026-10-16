using Timetracker.App.Models;
using Timetracker.App.ViewModels;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.App.Services;

/// <summary>
/// The tracker's tracking-state machine, exposed to add-ins as
/// <see cref="ITrackerStateQuery"/>. It mirrors the tracker view model's running
/// state and re-raises its start/stop events as typed state transitions, converting
/// the app's entry to the contract's <see cref="TrackedSession"/>.
/// </summary>
public sealed class TrackerStateMachine : ITrackerStateQuery
{
    private readonly TrackerViewModel _viewModel;

    public TrackerStateMachine(TrackerViewModel viewModel)
    {
        _viewModel = viewModel;
        State = viewModel.IsRunning ? TrackerState.Running : TrackerState.Stopped;
        _viewModel.SessionStarted += OnSessionStarted;
        _viewModel.SessionStopped += OnSessionStopped;
    }

    /// <inheritdoc/>
    public TrackerState State { get; private set; }

    /// <inheritdoc/>
    public event Action<TrackerStateChangedEventArgs>? StateChanged;

    private void OnSessionStarted(TrackerEntry entry)
    {
        State = TrackerState.Running;
        StateChanged?.Invoke(new TrackerStateChangedEventArgs(TrackerState.Running, TrackedSessionMapper.ToTracked(entry)));
    }

    private void OnSessionStopped(TrackerEntry entry)
    {
        State = TrackerState.Stopped;
        StateChanged?.Invoke(new TrackerStateChangedEventArgs(TrackerState.Stopped, TrackedSessionMapper.ToTracked(entry)));
    }
}
