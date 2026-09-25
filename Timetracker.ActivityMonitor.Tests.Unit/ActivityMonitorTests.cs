using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor.Tests.Unit;

public sealed class ActivityMonitorTests
{
    [Test]
    public void ActivityLog_WhenSpansAreAppended_ShouldReturnThemOrderedByFile()
    {
        var path = TempPath("log-append.json");
        File.Delete(path);
        var log = new ActivityLog(path);

        log.Add("active", At(9, 0), At(12, 0));
        log.Add("idle", At(12, 0), At(13, 30));

        var spans = log.GetAll();
        spans.Should().HaveCount(2);
        spans[0].Kind.Should().Be("active");
        spans[0].DurationText.Should().Be("3:00");
        spans[1].Kind.Should().Be("idle");
        spans[1].DurationText.Should().Be("1:30");
    }

    [Test]
    public void ActivityLog_WhenFileIsMissing_ShouldReturnAnEmptyList()
    {
        var log = new ActivityLog(TempPath("does-not-exist.json"));

        log.GetAll().Should().BeEmpty();
    }

    [Test]
    public void ActivityTracker_WhenIdlePeriodIsShort_ShouldIgnoreIt()
    {
        var path = TempPath("log-short-idle.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0), path + ".state.json");

        // 30 minutes idle: below the one-hour threshold, nothing is logged.
        tracker.PollWithDuration(TimeSpan.FromMinutes(30), At(9, 30));
        tracker.PollWithDuration(TimeSpan.FromMinutes(1), At(10, 0));

        log.GetAll().Should().BeEmpty("short breaks are ignored entirely");
    }

    [Test]
    public void ActivityTracker_WhenIdleLastsAtLeastOneHour_ShouldLogAnIdleSpan()
    {
        var path = TempPath("log-long-idle.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0), path + ".state.json");

        // 90 minutes without input: the idle span is recorded once input resumes.
        tracker.PollWithDuration(TimeSpan.FromMinutes(90), At(10, 30));
        tracker.PollWithDuration(TimeSpan.FromSeconds(5), At(10, 31));

        var spans = log.GetAll();
        spans.Should().HaveCount(2, "the active span and the idle span are recorded");
        spans[0].Kind.Should().Be("active");
        spans[1].Kind.Should().Be("idle");
        spans[1].Start.Should().Be(At(9, 0), "the idle span is back-dated to the last input");
        spans[1].End.Should().Be(At(10, 31), "the idle span ends when input resumes");
    }

    [Test]
    public void ActivityTracker_WhenStopped_ShouldWriteOpenSpanAndMarkStateOff()
    {
        var path = TempPath("log-stop.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var now = At(8, 0);
        var tracker = new TrackerForTests(log, () => now, path + ".state.json");

        tracker.Start();
        now = At(12, 0);
        tracker.Stop();

        var spans = log.GetAll();
        spans.Should().ContainSingle().Which.Kind.Should().Be("active");
        spans[0].End.Should().Be(At(12, 0));
        File.ReadAllText(path + ".state.json").Should().StartWith("off");
    }

    [Test]
    public void ActivityTracker_WhenStoppedTwice_ShouldNotWriteASecondSpan()
    {
        // Both the polling loop and ProcessExit call Stop; only one span may result.
        var path = TempPath("log-stop-twice.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var now = At(8, 0);
        var tracker = new TrackerForTests(log, () => now, path + ".state.json");

        tracker.Start();
        now = At(12, 0);
        tracker.Stop();
        tracker.Stop();

        log.GetAll().Should().ContainSingle("Stop must be idempotent");
    }

    [Test]
    public void ActivityTracker_WhenStartedAfterPreviousRun_ShouldCloseOpenSpan()
    {
        var path = TempPath("log-restart.json");
        File.Delete(path);
        var statePath = path + ".state.json";
        var log = new ActivityLog(path);

        // Simulate a previous run that died while active since 8:00.
        File.WriteAllText(statePath, "active\n" + At(8, 0).ToString("O"));

        var now = At(9, 15);
        var tracker = new TrackerForTests(log, () => now, statePath);
        tracker.Start();

        log.GetAll().Should().ContainSingle().Which.Should().Match<ActivitySpan>(s =>
            s.Kind == "active" && s.Start == At(8, 0) && s.End == At(9, 15));
    }

    [Test]
    public void MonitorSetupViewModel_WhenMonitorIsNotInstalled_ShouldOfferInstall()
    {
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = false }, new FakeWeekStatusHost());

        viewModel.InstallEnabled.Should().BeTrue("not installed yet, so Install is offered");
        viewModel.UninstallEnabled.Should().BeFalse();
    }

    [Test]
    public void MonitorSetupViewModel_WhenMonitorIsInstalled_ShouldOfferRemove()
    {
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, new FakeWeekStatusHost());

        viewModel.InstallEnabled.Should().BeFalse();
        viewModel.UninstallEnabled.Should().BeTrue("already installed, so Remove is offered");
    }

    [Test]
    public void MonitorSetupViewModel_WhenInstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var statusHost = new FakeWeekStatusHost();
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = false }, statusHost);

        viewModel.Install();

        viewModel.UninstallEnabled.Should().BeTrue("the install succeeded");
        statusHost.LastKind.Should().Be(WeekStatusKind.Success);
    }

    [Test]
    public void MonitorSetupViewModel_WhenUninstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var statusHost = new FakeWeekStatusHost();
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, statusHost);

        viewModel.Uninstall();

        viewModel.InstallEnabled.Should().BeTrue("the autostart was removed");
        statusHost.LastKind.Should().Be(WeekStatusKind.Success);
    }

    [Test]
    public void ActivityMonitorInstaller_WhenInstallCompletes_ShouldReportInstalled()
    {
        // Exercises the platform installer contract through the in-memory fake.
        var installer = new InMemoryInstaller { IsInstalled = false };

        installer.Install();

        installer.IsInstalled.Should().BeTrue();
        installer.MonitorExePath.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void WindowsIdleTimeProvider_WhenMeasuringIdleTime_ShouldUse32BitTickDifference()
    {
        // Normal case: 90 seconds between now and the last input.
        var current = unchecked((int)1_000_000u);
        var lastInput = unchecked((uint)(1_000_000 - 90_000));
        WindowsIdleTimeProvider.IdleMilliseconds(lastInput, current).Should().Be(90_000);

        // Tick counter wrapped past 2^32 (about every 49.7 days). The last input was
        // "before" the wrap, so a naive 64-bit subtraction would report ~49 days.
        uint wrappedLastInput = uint.MaxValue - 4_999;
        var wrappedCurrent = unchecked((int)5_000u);
        var idle = WindowsIdleTimeProvider.IdleMilliseconds(wrappedLastInput, wrappedCurrent);
        idle.Should().Be(10_000, "the 32-bit difference stays correct across the wrap");
        TimeSpan.FromMilliseconds(idle).Should().Be(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void WindowsIdleTimeProvider_WhenQueried_ShouldReportLastInputTick()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("GetLastInputInfo is a Win32 API");
        }

        // Exercises the real GetLastInputInfo binding. Its cbSize must be filled in,
        // otherwise the call fails and this would report zero idle forever.
        WindowsIdleTimeProvider.TryGetLastInputTick(out var tick).Should().BeTrue(
            "GetLastInputInfo succeeds when cbSize is set");

        var idle = WindowsIdleTimeProvider.IdleMilliseconds(tick, Environment.TickCount);
        idle.Should().BeLessThan(uint.MaxValue / 2,
            "the last input tick is a current value, not a stale or unset one");
    }

    [Test]
    public void MonitorLog_WhenCreated_ShouldWriteNextToTheExecutable()
    {
        MonitorLog.DefaultFilePath.Should().Be(
            Path.Combine(AppContext.BaseDirectory, "Timetracker.ActivityMonitor.log"));
    }

    [Test]
    public void MonitorLog_WhenEntryIsWritten_ShouldTagItWithCallingMethod()
    {
        var path = TempPath($"monitor-method-{Guid.NewGuid():N}.log");
        var monitorLog = new MonitorLog(path);

        WriteEntry(monitorLog, "hello");

        var text = File.ReadAllText(path);
        text.Should().Contain("[WriteEntry]", "the entry is tagged with the calling method");
        text.Should().Contain("hello");
    }

    [Test]
    public void ActivityTracker_WhenStarted_ShouldLogAStartupMessage()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-start");

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Start]");
        text.Should().Contain("Started;");
    }

    [Test]
    public void ActivityTracker_WhenStateFileIsMissing_ShouldLogIt()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-load-missing");

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[LoadState]");
        text.Should().Contain("No state file");
    }

    [Test]
    public void ActivityTracker_WhenStateFileHasUnparsableStartTime_ShouldLogIt()
    {
        var statePath = TempPath($"state-bad-start-{Guid.NewGuid():N}.json");
        File.WriteAllText(statePath, "active\nnot-a-timestamp");
        var monitorLogPath = TempPath($"monitor-bad-start-{Guid.NewGuid():N}.log");
        var tracker = new TrackerForTests(
            new ActivityLog(TempPath($"log-bad-start-{Guid.NewGuid():N}.json")),
            () => At(9, 0), statePath, new MonitorLog(monitorLogPath));

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[LoadState]");
        text.Should().Contain("unparsable start time");
    }

    [Test]
    public void ActivityTracker_WhenPolledPastInterval_ShouldLogOnePollingMessage()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-poll");
        tracker.Start();

        // Within the interval: no extra polling entry.
        tracker.PollAt(At(9, 1));
        File.ReadAllText(monitorLogPath).Should().NotContain("[Poll]", "the heartbeat is throttled");

        // Past the interval: one polling entry.
        tracker.PollAt(At(9, 6));
        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Poll]");
        text.Should().Contain("Poll;");
    }

    [Test]
    public void ActivityTracker_WhenStopped_ShouldLogAStoppingMessage()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-stop");
        tracker.Start();

        tracker.Stop();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Stop]");
        text.Should().Contain("Stopped;");
    }

    [Test]
    public void ActivityTracker_WhenStopped_ShouldLogTheSpanItClosed()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-stop-span");
        tracker.Start();

        tracker.Stop();

        File.ReadAllText(monitorLogPath).Should().Contain("closed \"active\" span",
            "the log names the span that was open, not the state it moved to");
    }

    private static void WriteEntry(MonitorLog monitorLog, string message) =>
        monitorLog.Info(message);

    private static (TrackerForTests Tracker, string LogPath) TrackerWithLog(string name)
    {
        var logPath = TempPath($"{name}-{Guid.NewGuid():N}.log");
        var tracker = new TrackerForTests(
            new ActivityLog(TempPath($"{name}-activity-{Guid.NewGuid():N}.json")),
            () => At(9, 0),
            TempPath($"{name}-state-{Guid.NewGuid():N}.json"),
            new MonitorLog(logPath));
        return (tracker, logPath);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 9, 21, hour, minute, 0, TimeSpan.FromHours(2));

    private static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "tt-activity-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    /// <summary>Test seam: idle time is injected instead of read from Win32.</summary>
    private sealed class TrackerForTests : ActivityTracker
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
    private sealed class InMemoryInstaller : IActivityMonitorInstaller
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
    private sealed class FakeWeekStatusHost : IWeekStatusHost
    {
        public string? LastMessage { get; private set; }

        public WeekStatusKind LastKind { get; private set; }

        public void ShowStatus(string message, WeekStatusKind kind)
        {
            LastMessage = message;
            LastKind = kind;
        }
    }
}
