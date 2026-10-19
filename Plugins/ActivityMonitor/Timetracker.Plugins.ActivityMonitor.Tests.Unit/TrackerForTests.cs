using Microsoft.Extensions.Logging;
using Timetracker.Plugins.ActivityMonitor.Services;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Test seam: idle time is injected instead of read from Win32.</summary>
internal sealed class TrackerForTests : ActivityTracker
{
    private readonly FakeIdleTimeProvider _idle;

    public TrackerForTests(
        ActivityLog log,
        Func<DateTimeOffset> now,
        string statePath,
        ILogger<ActivityTracker>? logger = null,
        TimeSpan? idleSpanThreshold = null)
        : this(log, now, new FakeIdleTimeProvider(), statePath, logger, idleSpanThreshold)
    {
    }

    private TrackerForTests(
        ActivityLog log,
        Func<DateTimeOffset> now,
        FakeIdleTimeProvider idle,
        string statePath,
        ILogger<ActivityTracker>? logger,
        TimeSpan? idleSpanThreshold)
        : base(log, now, idle, logger, idleSpanThreshold, statePath)
    {
        _idle = idle;
    }

    public void PollWithDuration(TimeSpan idle)
    {
        _idle.CurrentIdleTime = idle;
        Poll();
    }

    /// <summary>Polls with idle reported as unknown, to exercise the unknown path.</summary>
    public void PollWithUnknown()
    {
        _idle.CurrentIdleTime = null;
        Poll();
    }

    /// <summary>Polls at the current clock time, to exercise the log heartbeat.</summary>
    public void PollAt() => Poll();
}
