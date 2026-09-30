using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class GapDistributionTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Test]
    public void Plan_WhenTwoSessionsBorderTheGap_ShouldSplitItByTheirDurations()
    {
        var report = Session("Report", At(9, 0), At(10, 15));
        var review = Session("Review", At(11, 5), At(12, 0));

        var changes = GapDistribution.Plan(new TimeRange(At(10, 15), At(11, 5)), [report, review]);

        // 1:15 and 0:55 share 0:50 as 0:29 / 0:21 and meet at 10:44.
        changes.Should().HaveCount(2);
        changes[0].Original.Should().BeSameAs(report);
        changes[0].Updated!.End.Should().Be(At(10, 44));
        changes[0].Updated!.DurationSeconds.Should().Be(104 * 60, "the duration is re-derived from the times");
        changes[1].Original.Should().BeSameAs(review);
        changes[1].Updated!.Start.Should().Be(At(10, 44));
    }

    [Test]
    public void Plan_WhenOnlyTheEarlierSessionBordersTheGap_ShouldStretchItsEndOverTheWholeGap()
    {
        var report = Session("Report", At(9, 0), At(10, 0));

        var changes = GapDistribution.Plan(new TimeRange(At(10, 0), At(11, 0)), [report]);

        changes.Should().ContainSingle();
        changes[0].Updated!.Start.Should().Be(At(9, 0));
        changes[0].Updated!.End.Should().Be(At(11, 0));
    }

    [Test]
    public void Plan_WhenOnlyTheLaterSessionBordersTheGap_ShouldMoveItsStartToTheGapsStart()
    {
        var review = Session("Review", At(11, 0), At(12, 0));

        var changes = GapDistribution.Plan(new TimeRange(At(10, 0), At(11, 0)), [review]);

        changes.Should().ContainSingle();
        changes[0].Updated!.Start.Should().Be(At(10, 0));
        changes[0].Updated!.End.Should().Be(At(12, 0));
    }

    [Test]
    public void Plan_WhenNoSessionTouchesTheGap_ShouldPlanNothing()
    {
        // The session ends before the gap: stretching it would also bill the idle time in between.
        var report = Session("Report", At(8, 0), At(9, 0));

        var changes = GapDistribution.Plan(new TimeRange(At(10, 0), At(11, 0)), [report]);

        changes.Should().BeEmpty();
    }

    [Test]
    public void Plan_WhenTheSplitIsNoWholeMinute_ShouldGiveTheRemainderToTheLaterSession()
    {
        var report = Session("Report", At(9, 0), At(10, 0));
        var review = Session("Review", At(10, 7).AddSeconds(30), At(11, 7).AddSeconds(30));

        var changes = GapDistribution.Plan(new TimeRange(At(10, 0), At(10, 7).AddSeconds(30)), [report, review]);

        // Equal sessions share 7:30 as 4:00 (whole minutes) and 3:30 (the remainder).
        changes[0].Updated!.End.Should().Be(At(10, 4));
        changes[1].Updated!.Start.Should().Be(At(10, 4));
    }

    [Test]
    public void Plan_WhenPlanned_ShouldLeaveTheStoredSessionsUntouched()
    {
        var report = Session("Report", At(9, 0), At(10, 0));

        GapDistribution.Plan(new TimeRange(At(10, 0), At(11, 0)), [report]);

        report.End.Should().Be(At(10, 0), "the plan works on copies until it is applied");
    }

    private static DateTimeOffset At(int hour, int minute) => new(2026, 9, 21, hour, minute, 0, Offset);

    private static TrackerEntry Session(string task, DateTimeOffset start, DateTimeOffset end)
    {
        var session = new TrackerEntry { Task = task };
        session.Reschedule(start, end);
        return session;
    }
}
