using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>State query for tests: exposes a settable tracking state.</summary>
internal sealed class FakeTrackerStateQuery : ITrackerStateQuery
{
    private Action<TrackerStateChangedEventArgs>? _stateChanged;

    public TrackerState State { get; set; }

    public event Action<TrackerStateChangedEventArgs>? StateChanged
    {
        add => _stateChanged += value;
        remove => _stateChanged -= value;
    }
}
