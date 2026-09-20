using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.ActivityMonitor;
using Timetracker.Plugins;

namespace Timetracker.Tests;

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
    [Apartment(ApartmentState.STA)]
    public void SetupPanel_enables_the_button_that_matches_the_installer_state()
    {
        var notInstalled = new InMemoryInstaller { IsInstalled = false };
        var panel = new MonitorSetupPanel(notInstalled, new FakeWeekStatusHost());

        panel.InstallEnabled.Should().BeTrue("not installed yet, so Install is offered");
        panel.UninstallEnabled.Should().BeFalse();

        var installed = new InMemoryInstaller { IsInstalled = true };
        var installedPanel = new MonitorSetupPanel(installed, new FakeWeekStatusHost());

        installedPanel.InstallEnabled.Should().BeFalse();
        installedPanel.UninstallEnabled.Should().BeTrue("already installed, so Remove is offered");
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
        public TrackerForTests(ActivityLog log, Func<DateTimeOffset> now, string statePath)
            : base(log, now)
        {
            UseStateFile(statePath);
        }

        public void PollWithDuration(TimeSpan idle, DateTimeOffset at)
        {
            SetNow(() => at);
            PollIdleForTest(idle);
        }
    }

    /// <summary>In-memory installer so tests never touch the real HKCU Run key.</summary>
    private sealed class InMemoryInstaller : IActivityMonitorInstaller
    {
        public string MonitorExePath => @"C:\fake\Timetracker.ActivityMonitor.exe";

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

    /// <summary>Status sink that discards messages; the setup panel only writes to it.</summary>
    private sealed class FakeWeekStatusHost : IWeekStatusHost
    {
        public void ShowStatus(string message, WeekStatusKind kind)
        {
        }
    }
}
