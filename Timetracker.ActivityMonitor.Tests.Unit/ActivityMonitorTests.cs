using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor.Tests.Unit;

public sealed class ActivityMonitorTests
{
    [Test]
    public void Add_appends_spans_and_GetAll_returns_them_ordered_by_file()
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
    public void GetAll_on_a_missing_file_returns_an_empty_list()
    {
        var log = new ActivityLog(TempPath("does-not-exist.json"));

        log.GetAll().Should().BeEmpty();
    }

    [Test]
    public void Tracker_ignores_short_idle_periods()
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
    public void Tracker_logs_an_idle_span_of_at_least_one_hour()
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
    public void Stop_writes_the_open_span_and_marks_the_state_off()
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
    public void Stop_called_twice_does_not_write_a_second_span()
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
    public void Start_closes_the_span_left_open_by_the_previous_run()
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
    public void Setup_offers_install_when_the_monitor_is_not_installed()
    {
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = false }, new FakeWeekStatusHost());

        viewModel.InstallEnabled.Should().BeTrue("not installed yet, so Install is offered");
        viewModel.UninstallEnabled.Should().BeFalse();
    }

    [Test]
    public void Setup_offers_remove_when_the_monitor_is_installed()
    {
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, new FakeWeekStatusHost());

        viewModel.InstallEnabled.Should().BeFalse();
        viewModel.UninstallEnabled.Should().BeTrue("already installed, so Remove is offered");
    }

    [Test]
    public void Setup_install_flips_the_button_state_and_reports_success()
    {
        var statusHost = new FakeWeekStatusHost();
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = false }, statusHost);

        viewModel.Install();

        viewModel.UninstallEnabled.Should().BeTrue("the install succeeded");
        statusHost.LastKind.Should().Be(WeekStatusKind.Success);
    }

    [Test]
    public void Setup_uninstall_flips_the_button_state_and_reports_success()
    {
        var statusHost = new FakeWeekStatusHost();
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, statusHost);

        viewModel.Uninstall();

        viewModel.InstallEnabled.Should().BeTrue("the autostart was removed");
        statusHost.LastKind.Should().Be(WeekStatusKind.Success);
    }

    [Test]
    public void Installer_reports_that_it_is_installed_after_installing()
    {
        // Exercises the platform installer contract through the in-memory fake.
        var installer = new InMemoryInstaller { IsInstalled = false };

        installer.Install();

        installer.IsInstalled.Should().BeTrue();
        installer.MonitorExePath.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Windows_idle_time_uses_the_32_bit_tick_difference()
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
    public void Windows_idle_time_provider_reports_the_last_input_tick()
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
    public void Monitor_log_writes_next_to_the_executable()
    {
        MonitorLog.DefaultFilePath.Should().Be(
            Path.Combine(AppContext.BaseDirectory, "Timetracker.ActivityMonitor.log"));
    }

    [Test]
    public void Monitor_log_tags_entries_with_the_calling_method()
    {
        var path = TempPath($"monitor-method-{Guid.NewGuid():N}.log");
        var monitorLog = new MonitorLog(path);

        WriteEntry(monitorLog, "hello");

        var text = File.ReadAllText(path);
        text.Should().Contain("[WriteEntry]", "the entry is tagged with the calling method");
        text.Should().Contain("hello");
    }

    [Test]
    public void Start_logs_a_startup_message()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-start");

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Start]");
        text.Should().Contain("Started;");
    }

    [Test]
    public void Start_logs_when_the_state_file_is_missing()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-load-missing");

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[LoadState]");
        text.Should().Contain("No state file");
    }

    [Test]
    public void Start_logs_when_the_state_file_holds_an_unparsable_start_time()
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
    public void Poll_logs_a_polling_message_once_per_interval()
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
    public void Stop_logs_a_stopping_message()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-stop");
        tracker.Start();

        tracker.Stop();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Stop]");
        text.Should().Contain("Stopped;");
    }

    [Test]
    public void Stop_logs_the_span_it_closed()
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
