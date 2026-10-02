using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>
/// Idle auto-stop: when a session runs and there has been no keyboard/mouse input
/// for the threshold, the detector stops the session through the session host and
/// back-dates the end to the last input, so idle time is not billed.
/// </summary>
[TestFixture]
public sealed class IdleAutoStopTests
{
    [Test]
    public async Task PollAsync_WhenIdleReachesTheThreshold_ShouldStopTheSession()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = IdleAutoStop.IdleStopThreshold };
        var detector = new IdleAutoStop(host, host, idle);

        await detector.PollAsync();

        host.StopCalls.Should().Be(1, "30 minutes without input stops the session");
    }

    [Test]
    public async Task PollAsync_WhenIdleIsBelowTheThreshold_ShouldKeepTheSessionRunning()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider
        {
            CurrentIdleTime = IdleAutoStop.IdleStopThreshold - TimeSpan.FromSeconds(1),
        };
        var detector = new IdleAutoStop(host, host, idle);

        await detector.PollAsync();

        host.StopCalls.Should().Be(0, "the threshold is 30 minutes, not less");
    }

    [Test]
    public async Task PollAsync_WhenNoSessionIsRunning_ShouldNotStop()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = false };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = TimeSpan.FromHours(2) };
        var detector = new IdleAutoStop(host, host, idle);

        await detector.PollAsync();

        host.StopCalls.Should().Be(0, "nothing is running, so nothing is stopped");
    }

    [Test]
    public async Task PollAsync_WhenItStops_ShouldBackDateTheEndToTheLastInput()
    {
        var now = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.FromHours(2));
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = TimeSpan.FromMinutes(45) };
        var detector = new IdleAutoStop(host, host, idle, now: () => now);

        await detector.PollAsync();

        host.StoppedEndedAt.Should().Be(
            now - TimeSpan.FromMinutes(45), "the end is back-dated to the last input");
    }

    [Test]
    public async Task PollAsync_WhenItStops_ShouldExplainItInTheReason()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = TimeSpan.FromMinutes(40) };
        var detector = new IdleAutoStop(host, host, idle);

        await detector.PollAsync();

        host.StoppedReason.Should().Contain("40 min", "the idle duration is reported");
    }

    [Test]
    public async Task PollAsync_WhenThresholdIsConfiguredLower_ShouldStopEarlier()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = TimeSpan.FromMinutes(20) };
        var options = new InMemoryOptionsStore
        {
            Values = { [IdleOptions.IdleStopThresholdKey] = "15" },
        };
        var detector = new IdleAutoStop(host, host, idle, options: options);

        await detector.PollAsync();

        host.StopCalls.Should().Be(1, "the configured threshold of 15 minutes is reached at 20");
    }

    [Test]
    public async Task PollAsync_WhenTheConfiguredValueChanges_ShouldUseTheNewValueOnTheNextPoll()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = TimeSpan.FromMinutes(50) };
        var options = new InMemoryOptionsStore
        {
            Values = { [IdleOptions.IdleStopThresholdKey] = "60" },
        };
        var detector = new IdleAutoStop(host, host, idle, options: options);

        await detector.PollAsync();
        host.StopCalls.Should().Be(0, "50 minutes idle is below the configured 60");

        idle.CurrentIdleTime = TimeSpan.FromMinutes(70);
        options.Values[IdleOptions.IdleStopThresholdKey] = "45";
        await detector.PollAsync();

        host.StopCalls.Should().Be(1, "the user lowered the threshold to 45 without a restart");
    }

    [Test]
    public async Task PollAsync_WhenTheConfiguredValueIsInvalid_ShouldUseTheDefault()
    {
        var host = new FakeTrackerSessionHost { IsSessionRunning = true };
        var idle = new FakeIdleTimeProvider { CurrentIdleTime = TimeSpan.FromMinutes(25) };
        var options = new InMemoryOptionsStore
        {
            Values = { [IdleOptions.IdleStopThresholdKey] = "often" },
        };
        var detector = new IdleAutoStop(host, host, idle, options: options);

        await detector.PollAsync();

        host.StopCalls.Should().Be(0, "an unparsable value falls back to the 30-minute default");
    }
}
