using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Idle provider for tests: the idle duration is set directly, no platform API.</summary>
internal sealed class FakeIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan? CurrentIdleTime { get; set; }
}
