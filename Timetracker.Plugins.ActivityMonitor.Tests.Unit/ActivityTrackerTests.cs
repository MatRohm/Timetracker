using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;
using static Timetracker.Plugins.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

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
    public void Poll_WhenSpanThresholdIsConfiguredLower_ShouldLogShorterIdleSpans()
    {
        var path = TempPath("log-configured-span.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0), path + ".state.json")
        {
            IdleSpanThreshold = TimeSpan.FromMinutes(20),
        };

        // 30 minutes without input: below the default hour, above the configured threshold.
        tracker.PollWithDuration(TimeSpan.FromMinutes(30), At(9, 30));
        tracker.PollWithDuration(TimeSpan.FromSeconds(5), At(9, 31));

        var spans = log.GetAll();
        spans.Select(s => s.Kind).Should().Equal("active", "idle");
        spans[1].Start.Should().Be(At(9, 0), "the idle span is back-dated to the last input");
    }

    [Test]
    public void Poll_WhenSpanThresholdIsConfiguredHigher_ShouldIgnoreIdleBelowIt()
    {
        var path = TempPath("log-configured-span-high.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0), path + ".state.json")
        {
            IdleSpanThreshold = TimeSpan.FromMinutes(90),
        };

        // 45 minutes without input: below the configured 90-minute threshold.
        tracker.PollWithDuration(TimeSpan.FromMinutes(45), At(9, 45));
        tracker.PollWithDuration(TimeSpan.FromSeconds(5), At(9, 46));

        log.GetAll().Should().BeEmpty("45 idle minutes are below the configured threshold");
    }

    [Test]
    public void Poll_WhenNoThresholdIsConfigured_ShouldUseTheDefault()
    {
        var path = TempPath("log-default-span.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0), path + ".state.json");

        // 45 minutes without input: below the default one-hour threshold.
        tracker.PollWithDuration(TimeSpan.FromMinutes(45), At(9, 45));
        tracker.PollWithDuration(TimeSpan.FromSeconds(5), At(9, 46));

        log.GetAll().Should().BeEmpty("the fallback is the built-in one hour");
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
        var (tracker, logger) = TrackerWithLog("log-start");

        tracker.Start();

        Messages(logger).Should().Contain(m => m.StartsWith("Started;"));
    }

    [Test]
    public void Start_WhenStateFileIsMissing_ShouldLogIt()
    {
        var (tracker, logger) = TrackerWithLog("log-load-missing");

        tracker.Start();

        Messages(logger).Should().Contain(m => m.StartsWith("No state file"));
    }

    [Test]
    public void Start_WhenStateFileHasUnparsableStartTime_ShouldLogIt()
    {
        var statePath = TempPath($"state-bad-start-{Guid.NewGuid():N}.json");
        File.WriteAllText(statePath, "active\nnot-a-timestamp");
        var logger = new FakeLogger<ActivityTracker>();
        var tracker = new TrackerForTests(
            new ActivityLog(TempPath($"log-bad-start-{Guid.NewGuid():N}.json")),
            () => At(9, 0), statePath, logger);

        tracker.Start();

        logger.Collector.GetSnapshot().Should().Contain(r =>
            r.Level == LogLevel.Warning && r.Message.Contains("unparsable start time"));
    }

    [Test]
    public void Poll_WhenPolledPastInterval_ShouldLogOnePollingMessage()
    {
        var (tracker, logger) = TrackerWithLog("log-poll");
        tracker.Start();

        // Within the interval: no extra polling entry.
        tracker.PollAt(At(9, 1));
        Messages(logger).Should().NotContain(m => m.StartsWith("Poll;"), "the heartbeat is throttled");

        // Past the interval: one polling entry.
        tracker.PollAt(At(9, 6));
        Messages(logger).Should().ContainSingle(m => m.StartsWith("Poll;"));
    }

    [Test]
    public void Stop_WhenStopped_ShouldLogAStoppingMessage()
    {
        var (tracker, logger) = TrackerWithLog("log-stop");
        tracker.Start();

        tracker.Stop();

        Messages(logger).Should().Contain(m => m.StartsWith("Stopped;"));
    }

    [Test]
    public void Stop_WhenStopped_ShouldLogTheSpanItClosed()
    {
        var (tracker, logger) = TrackerWithLog("log-stop-span");
        tracker.Start();

        tracker.Stop();

        Messages(logger).Should().Contain(m => m.Contains("closed \"active\" span"),
            "the log names the span that was open, not the state it moved to");
    }

    [Test]
    public void Poll_WhenGapBelowThreshold_ShouldNotSplitTheOpenSpan()
    {
        var path = TempPath("log-sleep-short-gap.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0, 0), path + ".state.json");

        tracker.Start();
        tracker.PollAt(At(9, 0, 30)); // 30s later: below the one-minute threshold

        log.GetAll().Should().BeEmpty("a short wall-clock gap is not a sleep");
    }

    [Test]
    public void Poll_WhenWallClockJumpsPastThreshold_ShouldCloseTheSpanAtTheLastPollMoment()
    {
        var path = TempPath("log-sleep-long-gap.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0, 0), path + ".state.json");

        tracker.Start();
        tracker.PollAt(At(9, 0, 5)); // normal poll, no gap
        tracker.PollAt(At(11, 0, 0)); // ~2h later: the machine slept

        var spans = log.GetAll();
        spans.Should().ContainSingle("the sleep gap itself records nothing");
        spans[0].Kind.Should().Be("active");
        spans[0].Start.Should().Be(At(9, 0, 0));
        spans[0].End.Should().Be(At(9, 0, 5),
            "the span ends at the last observed moment, not across the sleep");
    }

    [Test]
    public void Poll_WhenWallClockJumpsPastThreshold_ShouldResumeActiveAtTheNewMoment()
    {
        var path = TempPath("log-sleep-resume.json");
        File.Delete(path);
        var log = new ActivityLog(path);
        var tracker = new TrackerForTests(log, () => At(9, 0, 0), path + ".state.json");

        tracker.Start();
        tracker.PollAt(At(9, 0, 5));
        tracker.PollAt(At(11, 0, 0)); // sleep detected: resume a fresh active span
        tracker.PollAt(At(11, 0, 30)); // normal poll after wake
        tracker.Stop(); // closes the resumed span at 11:00:30

        var spans = log.GetAll();
        spans.Should().HaveCount(2);
        spans[1].Kind.Should().Be("active");
        spans[1].Start.Should().Be(At(11, 0, 0), "a fresh active span starts on wake");
        spans[1].End.Should().Be(At(11, 0, 30));
    }

    private static (TrackerForTests Tracker, FakeLogger<ActivityTracker> Logger) TrackerWithLog(string name)
    {
        var logger = new FakeLogger<ActivityTracker>();
        var tracker = new TrackerForTests(
            new ActivityLog(TempPath($"{name}-activity-{Guid.NewGuid():N}.json")),
            () => At(9, 0),
            TempPath($"{name}-state-{Guid.NewGuid():N}.json"),
            logger);
        return (tracker, logger);
    }

    private static IReadOnlyList<string> Messages(FakeLogger<ActivityTracker> logger)
    {
        var result = logger.Collector.GetSnapshot().Select(r => r.Message).ToList();
        return result;
    }
}
