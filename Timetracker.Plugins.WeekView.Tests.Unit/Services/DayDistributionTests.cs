using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Services;

namespace Timetracker.Plugins.WeekView.Tests.Unit.Services;

[TestFixture]
public sealed class DayDistributionTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Test]
    public void Plan_WhenTasksHaveEqualDurations_ShouldSplitTheMissingTimeEvenly()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var review = Session("Review", At(10, 30), At(11, 30));

        var plan = DayDistribution.Plan([report, review], TimeSpan.FromMinutes(60), [report, review], null);

        plan.Tasks.Should().ContainSingle(t => t.Name == "Report")
            .Which.Share.Should().Be(TimeSpan.FromMinutes(30));
        plan.Tasks.Should().ContainSingle(t => t.Name == "Review")
            .Which.Share.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void Plan_WhenOneTaskIsLonger_ShouldGiveItAProportionallyLargerShare()
    {
        var report = Session("Report", At(9, 0), At(10, 30));
        var review = Session("Review", At(11, 0), At(11, 30));

        var plan = DayDistribution.Plan([report, review], TimeSpan.FromMinutes(60), [report, review], null);

        plan.Tasks.Should().ContainSingle(t => t.Name == "Report")
            .Which.Share.Should().Be(TimeSpan.FromMinutes(45));
        plan.Tasks.Should().ContainSingle(t => t.Name == "Review")
            .Which.Share.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Test]
    public void Plan_WhenATaskCanAbsorbItsShare_ShouldGrowItsLastSession()
    {
        var report = Session("Report", At(9, 0), At(10, 0));

        var plan = DayDistribution.Plan([report], TimeSpan.FromMinutes(30), [report], null);

        var change = plan.Changes.Should().ContainSingle().Which;
        change.Original.Should().BeSameAs(report);
        change.Updated!.Start.Should().Be(At(9, 0));
        change.Updated!.End.Should().Be(At(10, 30));
    }

    [Test]
    public void Plan_WhenATasksLastSessionIsBoxedIn_ShouldSkipTheTask()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var blocker = new[]
        {
            Session("Meeting", At(8, 0), At(9, 0)),
            Session("Meeting", At(10, 0), At(11, 0)),
        };
        var sessions = new[] { report, blocker[0], blocker[1] };

        var plan = DayDistribution.Plan(sessions, TimeSpan.FromMinutes(30), sessions, null);

        plan.Tasks.Should().ContainSingle(t => t.Name == "Meeting");
        plan.Skipped.Should().ContainSingle().Which.Name.Should().Be("Report");
    }

    [Test]
    public void Plan_WhenTheSplitIsNoWholeMinute_ShouldGiveTheRemainderToTheLastTask()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var review = Session("Review", At(10, 30), At(11, 30));

        var plan = DayDistribution.Plan([report, review], TimeSpan.FromMinutes(61), [report, review], null);

        plan.Tasks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Share)
            .Should().Be(TimeSpan.FromMinutes(61), "the shares add up to the missing time");
        plan.Tasks.Should().ContainSingle(t => t.Name == "Report")
            .Which.Share.Should().Be(TimeSpan.FromMinutes(31));
        plan.Tasks.Should().ContainSingle(t => t.Name == "Review")
            .Which.Share.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void Plan_WhenThereIsNoMissingTime_ShouldPlanNothing()
    {
        var report = Session("Report", At(9, 0), At(10, 0));

        var plan = DayDistribution.Plan([report], TimeSpan.Zero, [report], null);

        plan.HasWork.Should().BeFalse();
    }

    [Test]
    public void Plan_WhenThereAreNoSessions_ShouldPlanNothing()
    {
        var plan = DayDistribution.Plan([], TimeSpan.FromMinutes(30), [], null);

        plan.HasWork.Should().BeFalse();
    }

    [Test]
    public void Plan_WhenPlanned_ShouldLeaveTheStoredSessionsUntouched()
    {
        var report = Session("Report", At(9, 0), At(10, 0));

        DayDistribution.Plan([report], TimeSpan.FromMinutes(30), [report], null);

        report.Start.Should().Be(At(9, 0));
        report.End.Should().Be(At(10, 0));
    }

    [Test]
    public void Plan_WhenGivenExplicitShares_ShouldHandOutExactlyThoseShares()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var review = Session("Review", At(10, 30), At(11, 30));
        var adhoc = Session("Adhoc", At(12, 45), At(13, 15));
        var sessions = new[] { report, review, adhoc };
        var shares = new[]
        {
            new AssignedShare("Report", TimeSpan.FromMinutes(15)),
            new AssignedShare("Review", TimeSpan.FromMinutes(45)),
            new AssignedShare("Adhoc", TimeSpan.Zero),
        };

        var plan = DayDistribution.Plan(shares, sessions, sessions, null);

        plan.Tasks.Select(t => (t.Name, t.Share)).Should().Equal(
            ("Report", TimeSpan.FromMinutes(15)),
            ("Review", TimeSpan.FromMinutes(45)));
        plan.Tasks.Should().NotContain(t => t.Name == "Adhoc", "a zero share gets nothing");
        plan.Tasks.Single(t => t.Name == "Report").Changes.Should().ContainSingle()
            .Which.Updated!.End.Should().Be(At(10, 15));
        plan.Tasks.Single(t => t.Name == "Review").Changes.Should().ContainSingle()
            .Which.Updated!.End.Should().Be(At(12, 15));
    }

    [Test]
    public void Plan_WhenAnExplicitShareCannotBePlaced_ShouldSkipThatTask()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var sessions = new[]
        {
            Session("Meeting", At(8, 0), At(9, 0)),
            report,
            Session("Meeting", At(10, 0), At(11, 0)),
        };

        var plan = DayDistribution.Plan(
            [new AssignedShare("Report", TimeSpan.FromMinutes(15))], sessions, sessions, null);

        plan.Tasks.Should().BeEmpty();
        plan.Skipped.Should().ContainSingle().Which.Name.Should().Be("Report");
    }

    [Test]
    public void ProportionalShares_WhenSharesAreNotStepAligned_ShouldFloorToStepsAndGiveTheRemainderToTheLastTask()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var review = Session("Review", At(10, 30), At(12, 30));

        var shares = DayDistribution.ProportionalShares(
            [report, review], TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(15));

        shares.Select(s => (s.Task, s.Share)).Should().Equal(
            ("Report", TimeSpan.FromMinutes(15)),
            ("Review", TimeSpan.FromMinutes(35)));
    }

    [Test]
    public void ProportionalShares_WhenMissingIsSmallerThanTheStep_ShouldGiveTheRemainderToTheLastTask()
    {
        var report = Session("Report", At(9, 0), At(10, 0));

        var shares = DayDistribution.ProportionalShares(
            [report], TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        shares.Select(s => (s.Task, s.Share)).Should().Equal(("Report", TimeSpan.FromMinutes(5)));
    }

    private static DateTimeOffset At(int hour, int minute) => new(2026, 9, 21, hour, minute, 0, Offset);

    private static TrackedSession Session(string task, DateTimeOffset start, DateTimeOffset end)
    {
        var result = new TrackedSession { Task = task }.Reschedule(start, end);
        return result;
    }
}
