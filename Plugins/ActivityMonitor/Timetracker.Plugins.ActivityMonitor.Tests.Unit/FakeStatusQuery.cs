using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Models;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Status query whose result is set directly; never talks to a real monitor.</summary>
internal sealed class FakeStatusQuery : IActivityMonitorStatusQuery
{
    public MonitorStatus Status { get; set; } = MonitorStatus.Unknown;

    public Task<MonitorStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);
}
