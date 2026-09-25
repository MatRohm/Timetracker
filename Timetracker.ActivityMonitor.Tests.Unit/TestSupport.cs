using Timetracker.ActivityMonitor.Interfaces;
using Timetracker.Plugins.Interfaces;
using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor.Tests.Unit;

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
        ActivityLog log, Func<DateTimeOffset> now, string statePath, MonitorLog? monitorLog = null)
        : base(log, now, new NullIdleTimeProvider(), monitorLog)
    {
        UseStateFile(statePath);
    }

    public void PollWithDuration(TimeSpan idle, DateTimeOffset at)
    {
        SetNow(() => at);
        PollIdleForTest(idle);
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
        Path.GetTempPath(), "Timetracker.ActivityMonitor");

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

/// <summary>Status sink that records the last message; the setup view model only writes to it.</summary>
internal sealed class FakeWeekStatusHost : IWeekStatusHost
{
    public string? LastMessage { get; private set; }

    public WeekStatusKind LastKind { get; private set; }

    public void ShowStatus(string message, WeekStatusKind kind)
    {
        LastMessage = message;
        LastKind = kind;
    }
}
