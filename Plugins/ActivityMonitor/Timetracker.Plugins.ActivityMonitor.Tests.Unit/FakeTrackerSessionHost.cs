using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Session host for tests: records the stop request the idle detector issues.</summary>
internal sealed class FakeTrackerSessionHost : ITrackerSessionQuery, ITrackerSessionCommand
{
    public bool IsSessionRunning { get; set; }

    public DateTimeOffset? StoppedEndedAt { get; private set; }

    public string? StoppedReason { get; private set; }

    public int StopCalls { get; private set; }

    public Task<bool> StartSessionAsync(string taskName, string bookingElement, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<bool> StopSessionAsync(DateTimeOffset? endedAt, string? reason, CancellationToken cancellationToken)
    {
        StopCalls++;
        StoppedEndedAt = endedAt;
        StoppedReason = reason;
        return Task.FromResult(true);
    }
}
