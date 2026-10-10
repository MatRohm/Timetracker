using Microsoft.Extensions.Logging;
using Timetracker.Plugins.ActivityMonitor.Services;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Test seam: idle time is injected instead of read from Win32.</summary>
internal sealed class TrackerForTests : ActivityTracker
{
    public TrackerForTests(
        ActivityLog log, Func<DateTimeOffset> now, string statePath, ILogger<ActivityTracker>? logger = null)
        : base(log, now, new NullIdleTimeProvider(), logger)
    {
        UseStateFile(statePath);
    }

    public void PollWithDuration(TimeSpan idle, DateTimeOffset at)
    {
        SetNow(() => at);
        PollIdleForTest(idle);
    }

    /// <summary>Polls with idle reported as unknown, to exercise the unknown path.</summary>
    public void PollWithUnknown(DateTimeOffset at)
    {
        SetNow(() => at);
        PollIdleForTest(null);
    }

    /// <summary>Polls at a given moment with no idle, to exercise the log heartbeat.</summary>
    public void PollAt(DateTimeOffset at)
    {
        SetNow(() => at);
        Poll();
    }
}
