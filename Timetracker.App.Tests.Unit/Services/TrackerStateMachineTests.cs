using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Services;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Tests.Unit.Services;

/// <summary>
/// The tracking state machine mirrors the tracker's running state and re-raises the
/// view model's start/stop as typed transitions with the concerned session.
/// </summary>
[TestFixture]
public sealed class TrackerStateMachineTests
{
    [Test]
    public void State_WhenNoSessionIsRunning_ShouldBeStopped()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);

        var machine = new TrackerStateMachine(vm);

        machine.State.Should().Be(TrackerState.Stopped);
    }

    [Test]
    public void State_WhenASessionStarts_ShouldBeRunning()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        var machine = new TrackerStateMachine(vm);

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        machine.State.Should().Be(TrackerState.Running);
    }

    [Test]
    public async Task State_WhenStartedThenStopped_ShouldNotifyRunningThenStoppedWithTheEntries()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        var machine = new TrackerStateMachine(vm);

        var notifications = new List<TrackerStateChangedEventArgs>();
        machine.StateChanged += notifications.Add;

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        await vm.StopSessionAsync();

        notifications.Should().HaveCount(2);
        notifications[0].State.Should().Be(TrackerState.Running);
        notifications[0].Session.Task.Should().Be("Report");
        notifications[0].Session.Id.Should().Be(Guid.Empty);
        notifications[0].Session.End.Should().Be(default);
        notifications[0].Session.Duration.Should().Be("00:00:00");
        notifications[1].State.Should().Be(TrackerState.Stopped);
        notifications[1].Session.Task.Should().Be("Report");
        notifications[1].Session.Id.Should().NotBe(Guid.Empty);
        machine.State.Should().Be(TrackerState.Stopped);
    }
}
