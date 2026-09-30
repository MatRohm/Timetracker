using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Services;

namespace Timetracker.Plugins.WeekView.Tests.Unit.Services;

[TestFixture]
public sealed class HalfHourRoundingTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Test]
    public void Plan_WhenATaskIsMoreThanAQuarterPastAHalfHour_ShouldRoundUpByMovingTheEnd()
    {
        var report = Session("Report", At(10, 0), At(10, 50));

        var plan = HalfHourRounding.Plan([report], [report], running: null);

        plan.Changes.Should().ContainSingle();
        plan.Changes[0].Updated!.End.Should().Be(At(11, 0));
        plan.Tasks.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Name = "Report", Total = TimeSpan.FromMinutes(50), Target = TimeSpan.FromHours(1) });
    }

    [Test]
    public void Plan_WhenATaskIsLessThanAQuarterPastAHalfHour_ShouldRoundDownByShorteningTheEnd()
    {
        var report = Session("Report", At(10, 0), At(12, 40));

        var plan = HalfHourRounding.Plan([report], [report], running: null);

        plan.Changes.Single().Updated!.End.Should().Be(At(12, 30));
    }

    [TestCase(45, 60)]
    [TestCase(75, 90)]
    public void Plan_WhenATaskIsExactlyAQuarterPastAHalfHour_ShouldRoundUp(int minutes, int expected)
    {
        var report = Session("Report", At(10, 0), At(10, 0).AddMinutes(minutes));

        var plan = HalfHourRounding.Plan([report], [report], running: null);

        plan.Changes.Single().Updated!.End.Should().Be(At(10, 0).AddMinutes(expected));
    }

    [Test]
    public void Plan_WhenATaskIsUnderFifteenMinutes_ShouldRoundUpToHalfAnHour()
    {
        var report = Session("Report", At(10, 0), At(10, 10));

        var plan = HalfHourRounding.Plan([report], [report], running: null);

        plan.Changes.Single().Updated!.End.Should().Be(At(10, 30), "nothing booked disappears through rounding");
    }

    [Test]
    public void Plan_WhenEveryTaskIsOnAHalfHour_ShouldHaveNoWork()
    {
        var report = Session("Report", At(9, 0), At(10, 30));

        var plan = HalfHourRounding.Plan([report], [report], running: null);

        plan.HasWork.Should().BeFalse();
    }

    [Test]
    public void Plan_WhenTheEndWouldCollideWithTheNextSession_ShouldMoveTheStartEarlierInstead()
    {
        var report = Session("Report", At(10, 0), At(10, 50));
        var review = Session("Review", At(10, 50), At(11, 20));

        var plan = HalfHourRounding.Plan([report, review], [report, review], running: null);

        var change = plan.Changes.Single();
        change.Original.Should().BeSameAs(report);
        change.Updated!.Start.Should().Be(At(9, 50));
        change.Updated!.End.Should().Be(At(10, 50));
    }

    [Test]
    public void Plan_WhenBothSidesOfTheLastSessionAreTaken_ShouldSkipTheTaskAndSayWhy()
    {
        var before = Session("Standup", At(9, 30), At(10, 0));
        var report = Session("Report", At(10, 0), At(10, 50));
        var after = Session("Review", At(10, 50), At(11, 20));

        var plan = HalfHourRounding.Plan([before, report, after], [before, report, after], running: null);

        plan.Changes.Should().BeEmpty();
        plan.Skipped.Should().Equal(new SkippedTask("Report", TimeSpan.FromMinutes(50)));
        plan.HasWork.Should().BeTrue("the task is off the half hour, so the user should learn why nothing happened");
    }

    [Test]
    public void Plan_WhenATimerRunsRightAfterTheTask_ShouldNotExtendIntoIt()
    {
        var report = Session("Report", At(10, 0), At(10, 50));
        var running = new TimeRange(At(10, 50), DateTimeOffset.MaxValue);

        var plan = HalfHourRounding.Plan([report], [report], running);

        plan.Changes.Single().Updated!.Start.Should().Be(At(9, 50), "the end is blocked by the running timer");
    }

    [Test]
    public void Plan_WhenShorteningReachesAnEarlierSession_ShouldRemoveOnesThatShrinkToNothing()
    {
        var morning = Session("Report", At(9, 0), At(10, 0));
        var tail = Session("Report", At(11, 0), At(11, 10));

        var plan = HalfHourRounding.Plan([morning, tail], [morning, tail], running: null);

        // 1:10 rounds down to 1:00: the 10-minute tail session is removed entirely.
        var change = plan.Changes.Single();
        change.Original.Should().BeSameAs(tail);
        change.Updated.Should().BeNull();
    }

    [Test]
    public void Plan_WhenTasksDifferOnlyByCase_ShouldRoundTheirSumOnce()
    {
        var first = Session("report", At(10, 0), At(10, 20));
        var second = Session("Report", At(11, 0), At(11, 20));

        var plan = HalfHourRounding.Plan([first, second], [first, second], running: null);

        // 0:40 rounds down to 0:30 by shortening the later session.
        plan.Tasks.Should().ContainSingle().Which.Total.Should().Be(TimeSpan.FromMinutes(40));
        plan.Changes.Single().Updated!.End.Should().Be(At(11, 10));
    }

    [Test]
    public void Plan_WhenTwoTasksWantTheSameFreeTime_ShouldGiveItToTheEarlierTaskOnly()
    {
        var first = Session("First", At(10, 0), At(10, 50));
        var second = Session("Second", At(11, 0), At(11, 50));
        var third = Session("Third", At(11, 50), At(12, 20));
        var all = new[] { first, second, third };

        var plan = HalfHourRounding.Plan(all, all, running: null);

        // First takes 10:50–11:00; Second can neither extend into Third nor into First's new time.
        plan.Changes.Single().Original.Should().BeSameAs(first);
        plan.Changes.Single().Updated!.End.Should().Be(At(11, 0));
        plan.Skipped.Should().ContainSingle().Which.Name.Should().Be("Second");
    }

    private static DateTimeOffset At(int hour, int minute) => new(2026, 9, 21, hour, minute, 0, Offset);

    private static TrackedSession Session(string task, DateTimeOffset start, DateTimeOffset end)
    {
        var result = new TrackedSession { Task = task }.Reschedule(start, end);
        return result;
    }
}
