using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

/// <summary>
/// Idle auto-stop: when there has been no keyboard/mouse input for the threshold,
/// the running session is stopped and recorded up to the moment the idle stretch
/// began, so idle time is not billed to the task.
/// </summary>
public sealed class IdleStopTests
{
    [Test]
    public void A_running_session_stops_once_the_idle_threshold_is_reached()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.IsRunning.Should().BeTrue();

        idle.CurrentIdleTime = TrackerViewModel.IdleStopThreshold;
        timer.RaiseTick();

        vm.IsRunning.Should().BeFalse("30 minutes without input stops the timer");
        repo.GetAll().Should().ContainSingle("the session was saved");
    }

    [Test]
    public void Idle_below_the_threshold_keeps_the_session_running()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        idle.CurrentIdleTime = TrackerViewModel.IdleStopThreshold - TimeSpan.FromSeconds(1);
        timer.RaiseTick();

        vm.IsRunning.Should().BeTrue("the threshold is 30 minutes, not less");
        repo.GetAll().Should().BeEmpty();
    }

    [Test]
    public void The_idle_span_is_not_counted_as_work_time()
    {
        // A 15-minute session, then 45 minutes away: only the 15 worked minutes
        // are billed, and the entry ends when the user walked away.
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        var now = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.FromHours(2));
        using var vm = new TrackerViewModel(repo, timer, idle, () => now);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        // 15 minutes of work, then the machine sits idle for 45 minutes.
        var worked = TimeSpan.FromMinutes(15);
        var idleFor = TimeSpan.FromMinutes(45);
        now += worked + idleFor;
        idle.CurrentIdleTime = idleFor;
        timer.RaiseTick();

        var entry = repo.GetAll().Single();
        entry.DurationSeconds.Should().Be(worked.TotalSeconds, "only worked time is billed");
        entry.Duration.Should().Be("00:15:00");
        entry.End.Should().Be(now - idleFor, "the entry ends when the idle stretch began");
    }

    [Test]
    public void The_status_line_explains_the_idle_stop()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        idle.CurrentIdleTime = TimeSpan.FromMinutes(40);
        timer.RaiseTick();

        vm.StatusText.Should().Contain("40 min", "the idle duration is reported");
        vm.Status.Should().Be(TrackerStatus.Info);
    }

    [Test]
    public void A_stopped_session_is_not_stopped_again_on_later_ticks()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        idle.CurrentIdleTime = TimeSpan.FromMinutes(45);
        timer.RaiseTick();
        timer.RaiseTick();
        timer.RaiseTick();

        repo.GetAll().Should().ContainSingle("only one entry is saved");
    }

    [Test]
    public void Idle_does_not_stop_a_session_that_is_not_running()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);

        idle.CurrentIdleTime = TimeSpan.FromHours(2);
        timer.RaiseTick();

        vm.IsRunning.Should().BeFalse();
        repo.GetAll().Should().BeEmpty("nothing was running, so nothing is saved");
    }

    [Test]
    public void Starting_again_after_an_idle_stop_works_normally()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        idle.CurrentIdleTime = TimeSpan.FromMinutes(45);
        timer.RaiseTick();

        // Back at the machine: idle resets and a new session can start.
        idle.CurrentIdleTime = TimeSpan.Zero;
        vm.StartCommand.Execute(null);
        vm.IsRunning.Should().BeTrue();
        vm.StopCommand.Execute(null);

        repo.GetAll().Should().HaveCount(2, "the idle-stopped session and the new one");
    }
}
