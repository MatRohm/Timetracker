using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Controller for tests: the start/stop result is set directly, no process is launched.</summary>
internal sealed class FakeActivityMonitorController : IActivityMonitorController
{
    public bool StartResult { get; set; } = true;
    public bool StopResult { get; set; } = true;
    public int StartCalls { get; private set; }
    public int StopCalls { get; private set; }

    public bool Start()
    {
        StartCalls++;
        return StartResult;
    }

    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        StopCalls++;
        return Task.FromResult(StopResult);
    }
}
