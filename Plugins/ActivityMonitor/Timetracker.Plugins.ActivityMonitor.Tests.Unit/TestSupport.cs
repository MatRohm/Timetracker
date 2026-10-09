using Microsoft.Extensions.Logging;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Models;
using Timetracker.Plugins.ActivityMonitor.Services;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Shared helpers for the activity monitor unit tests.</summary>
internal static class TestSupport
{
    internal static DateTimeOffset At(int hour, int minute) =>
        new(2026, 9, 21, hour, minute, 0, TimeSpan.FromHours(2));

    internal static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "tt-activity-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}

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

/// <summary>In-memory installer so tests never touch the real HKCU Run key.</summary>
internal sealed class InMemoryInstaller : IActivityMonitorInstaller
{
    public string MonitorExePath => Path.Combine(
        Path.GetTempPath(), "Timetracker.Plugins.ActivityMonitor");

    public bool IsInstalled { get; set; }

    public bool Install()
    {
        IsInstalled = true;
        return true;
    }

    public bool Uninstall()
    {
        IsInstalled = false;
        return true;
    }
}

/// <summary>Status query whose result is set directly; never talks to a real monitor.</summary>
internal sealed class FakeStatusQuery : IActivityMonitorStatusQuery
{
    public MonitorStatus Status { get; set; } = MonitorStatus.Unknown;

    public Task<MonitorStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);
}

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

/// <summary>Idle provider for tests: the idle duration is set directly, no platform API.</summary>
internal sealed class FakeIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan? CurrentIdleTime { get; set; }
}

/// <summary>Options kept in memory; tests set <see cref="Values"/> directly.</summary>
internal sealed class InMemoryOptionsStore : IOptionQuery
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetValue(string key) => Values.TryGetValue(key, out var value) ? value : null;
}

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
