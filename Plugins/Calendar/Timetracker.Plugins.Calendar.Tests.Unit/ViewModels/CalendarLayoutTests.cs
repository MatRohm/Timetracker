using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.Tests.Unit.ViewModels;

[TestFixture]
public sealed class CalendarLayoutTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));

    [Test]
    public void FitAxis_WhenThereIsNothingToShow_ShouldReturnTheDefaultWindow()
    {
        var axis = CalendarLayout.FitAxis([]);

        axis.Should().Be(CalendarAxis.Default);
        axis.Start.Should().Be(TimeSpan.FromHours(8));
        axis.End.Should().Be(TimeSpan.FromHours(18));
    }

    [Test]
    public void FitAxis_WhenTheDataIsLongEnough_ShouldSnapTheBoundsToHalfHours()
    {
        var axis = CalendarLayout.FitAxis([Range(8, 10, 17, 50)]);

        axis.Start.Should().Be(TimeSpan.FromHours(8));
        axis.End.Should().Be(TimeSpan.FromHours(18));
    }

    [Test]
    public void FitAxis_WhenTheDataIsShort_ShouldKeepTheMinimumWindowAroundIt()
    {
        var axis = CalendarLayout.FitAxis([Range(10, 0, 10, 30)]);

        axis.Start.Should().Be(TimeSpan.FromHours(6));
        axis.End.Should().Be(TimeSpan.FromHours(14));
        axis.Duration.Should().Be(CalendarLayout.MinimumWindow);
    }

    [Test]
    public void FitAxis_WhenTheDataIsNearMidnight_ShouldClampToTheDay()
    {
        var axis = CalendarLayout.FitAxis([Range(22, 0, 23, 30)]);

        axis.Start.Should().Be(TimeSpan.FromHours(16));
        axis.End.Should().Be(TimeSpan.FromHours(24));
    }

    [Test]
    public void FitAxis_WhenNowIsGiven_ShouldIncludeItInTheWindow()
    {
        var axis = CalendarLayout.FitAxis([Range(9, 0, 10, 0)], TimeSpan.FromHours(20));

        axis.Start.Should().Be(TimeSpan.FromHours(9));
        axis.End.Should().Be(TimeSpan.FromHours(20));
    }

    [Test]
    public void AssignLanes_WhenBlocksDoNotOverlap_ShouldPutThemInOneLane()
    {
        var lanes = CalendarLayout.AssignLanes([Range(9, 0, 10, 0), Range(10, 0, 11, 0)]);

        lanes.Should().OnlyContain(l => l.Lane == 0 && l.LaneCount == 1);
    }

    [Test]
    public void AssignLanes_WhenTwoBlocksOverlap_ShouldPutThemSideBySide()
    {
        var lanes = CalendarLayout.AssignLanes([Range(9, 0, 10, 30), Range(10, 0, 11, 0)]);

        lanes.Select(l => l.Lane).Should().Equal(0, 1);
        lanes.Should().OnlyContain(l => l.LaneCount == 2);
    }

    [Test]
    public void AssignLanes_WhenABlockEndsBeforeTheNextStarts_ShouldReuseTheLane()
    {
        var lanes = CalendarLayout.AssignLanes(
        [
            Range(9, 0, 11, 0),
            Range(9, 30, 10, 30),
            Range(11, 0, 12, 0),
        ]);

        lanes.Select(l => l.Lane).Should().Equal(0, 1, 0);
        lanes.Take(2).Should().OnlyContain(l => l.LaneCount == 2);
        lanes[2].LaneCount.Should().Be(1, "the third block starts after the cluster ended");
    }

    [Test]
    public void AssignLanes_WhenAClusterHasThreeLanes_ShouldCountThemOnce()
    {
        var lanes = CalendarLayout.AssignLanes(
        [
            Range(9, 0, 12, 0),
            Range(9, 30, 11, 0),
            Range(10, 0, 10, 30),
        ]);

        lanes.Select(l => l.Lane).Should().Equal(0, 1, 2);
        lanes.Should().OnlyContain(l => l.LaneCount == 3);
    }

    [Test]
    public void Top_WhenTheTimeIsInsideTheAxis_ShouldScaleItFromTheStart()
    {
        var axis = new CalendarAxis(TimeSpan.FromHours(9), TimeSpan.FromHours(17));

        var top = CalendarLayout.Top(TimeSpan.FromHours(10) + TimeSpan.FromMinutes(30), axis);

        top.Should().Be(1.5 * CalendarLayout.PixelsPerHour);
    }

    [Test]
    public void Top_WhenTheTimeIsOutsideTheAxis_ShouldClampToTheEdge()
    {
        var axis = new CalendarAxis(TimeSpan.FromHours(9), TimeSpan.FromHours(17));

        CalendarLayout.Top(TimeSpan.FromHours(8), axis).Should().Be(0);
        CalendarLayout.Top(TimeSpan.FromHours(18), axis).Should().Be(axis.Height);
    }

    [Test]
    public void Height_WhenADurationIsInsideTheAxis_ShouldMatchTheTimeScale()
    {
        var axis = new CalendarAxis(TimeSpan.FromHours(9), TimeSpan.FromHours(17));

        var height = CalendarLayout.Height(TimeSpan.FromHours(10), TimeSpan.FromHours(11), axis);

        height.Should().Be(CalendarLayout.PixelsPerHour);
    }

    [Test]
    public void Height_WhenTheBlockCrossesAnAxisEdge_ShouldClipIt()
    {
        var axis = new CalendarAxis(TimeSpan.FromHours(9), TimeSpan.FromHours(17));

        var height = CalendarLayout.Height(TimeSpan.FromHours(8), TimeSpan.FromHours(9) + TimeSpan.FromMinutes(30), axis);

        height.Should().Be(0.5 * CalendarLayout.PixelsPerHour);
    }

    private static TimeRange Range(int startHour, int startMinute, int endHour, int endMinute) =>
        new(Day.AddHours(startHour).AddMinutes(startMinute), Day.AddHours(endHour).AddMinutes(endMinute));
}
