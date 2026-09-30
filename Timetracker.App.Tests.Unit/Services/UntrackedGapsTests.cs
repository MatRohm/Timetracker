using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class UntrackedGapsTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);
    private static readonly TimeSpan Minimum = TimeSpan.FromMinutes(15);

    [Test]
    public void Find_WhenSessionsLeaveHolesInAnActiveSpan_ShouldReturnTheHolesBeforeBetweenAndAfter()
    {
        var gaps = UntrackedGaps.Find(
            [Range(9, 0, 13, 0)],
            [Session(9, 30, 10, 0), Session(11, 0, 12, 0)],
            running: null,
            Minimum);

        gaps.Should().Equal(Range(9, 0, 9, 30), Range(10, 0, 11, 0), Range(12, 0, 13, 0));
    }

    [Test]
    public void Find_WhenASessionReachesOutsideTheActiveSpan_ShouldOnlySubtractTheOverlap()
    {
        var gaps = UntrackedGaps.Find([Range(9, 0, 12, 0)], [Session(8, 0, 10, 0)], running: null, Minimum);

        gaps.Should().Equal(Range(10, 0, 12, 0));
    }

    [Test]
    public void Find_WhenActiveSpansOverlap_ShouldMergeThemIntoOneGap()
    {
        var gaps = UntrackedGaps.Find([Range(9, 30, 11, 0), Range(9, 0, 10, 0)], [], running: null, Minimum);

        gaps.Should().Equal(Range(9, 0, 11, 0));
    }

    [Test]
    public void Find_WhenAGapIsShorterThanTheMinimum_ShouldDropIt()
    {
        var gaps = UntrackedGaps.Find([Range(9, 0, 10, 0)], [Session(9, 10, 10, 0)], running: null, Minimum);

        gaps.Should().BeEmpty("a 10-minute hole is below the 15-minute minimum");
    }

    [Test]
    public void Find_WhenATimerIsRunning_ShouldNotReportItsTimeAsAGap()
    {
        var running = new TimeRange(At(10, 0), DateTimeOffset.MaxValue);

        var gaps = UntrackedGaps.Find([Range(9, 0, 12, 0)], [], running, Minimum);

        gaps.Should().Equal(Range(9, 0, 10, 0));
    }

    [Test]
    public void Find_WhenASessionFromThePreviousEveningRunsPastMidnight_ShouldCoverTheEarlyHours()
    {
        var evening = new TrackerEntry { Task = "Deploy" };
        evening.Reschedule(At(23, 0).AddDays(-1), At(1, 0));

        var gaps = UntrackedGaps.Find([Range(0, 0, 2, 0)], [evening], running: null, Minimum);

        gaps.Should().Equal(Range(1, 0, 2, 0));
    }

    [Test]
    public void Find_WhenNoActivityWasRecorded_ShouldReturnNoGaps()
    {
        var gaps = UntrackedGaps.Find([], [Session(9, 0, 10, 0)], running: null, Minimum);

        gaps.Should().BeEmpty();
    }

    private static DateTimeOffset At(int hour, int minute) => new(2026, 9, 21, hour, minute, 0, Offset);

    private static TimeRange Range(int startHour, int startMinute, int endHour, int endMinute) =>
        new(At(startHour, startMinute), At(endHour, endMinute));

    private static TrackerEntry Session(int startHour, int startMinute, int endHour, int endMinute)
    {
        var session = new TrackerEntry { Task = "Report" };
        session.Reschedule(At(startHour, startMinute), At(endHour, endMinute));
        return session;
    }
}
