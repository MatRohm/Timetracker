using AwesomeAssertions;
using NUnit.Framework;
using static Timetracker.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class ActivityTrackerTests
{
    [Test]
    public void Poll_WhenIdlePeriodIsShort_ShouldIgnoreIt()
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
    public void Poll_WhenIdleLastsAtLeastOneHour_ShouldLogAnIdleSpan()
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
    public void Stop_WhenStopped_ShouldWriteOpenSpanAndMarkStateOff()
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
    public void Stop_WhenStoppedTwice_ShouldNotWriteASecondSpan()
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
    public void Start_WhenStartedAfterPreviousRun_ShouldCloseOpenSpan()
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
    public void Start_WhenStarted_ShouldLogAStartupMessage()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-start");

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Start]");
        text.Should().Contain("Started;");
    }

    [Test]
    public void Start_WhenStateFileIsMissing_ShouldLogIt()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-load-missing");

        tracker.Start();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[LoadState]");
        text.Should().Contain("No state file");
    }

    [Test]
    public void Start_WhenStateFileHasUnparsableStartTime_ShouldLogIt()
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
    public void Poll_WhenPolledPastInterval_ShouldLogOnePollingMessage()
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
    public void Stop_WhenStopped_ShouldLogAStoppingMessage()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-stop");
        tracker.Start();

        tracker.Stop();

        var text = File.ReadAllText(monitorLogPath);
        text.Should().Contain("[Stop]");
        text.Should().Contain("Stopped;");
    }

    [Test]
    public void Stop_WhenStopped_ShouldLogTheSpanItClosed()
    {
        var (tracker, monitorLogPath) = TrackerWithLog("log-stop-span");
        tracker.Start();

        tracker.Stop();

        File.ReadAllText(monitorLogPath).Should().Contain("closed \"active\" span",
            "the log names the span that was open, not the state it moved to");
    }

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
}
