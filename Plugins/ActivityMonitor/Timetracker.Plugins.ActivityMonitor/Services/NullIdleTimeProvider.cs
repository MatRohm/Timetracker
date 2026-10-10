using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>Reports idle as unknown; used when no platform API is available.</summary>
internal sealed class NullIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan? CurrentIdleTime => null;
}
