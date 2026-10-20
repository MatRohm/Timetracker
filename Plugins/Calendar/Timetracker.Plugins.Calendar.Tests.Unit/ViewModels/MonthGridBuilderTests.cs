using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.Tests.Unit.ViewModels;

[TestFixture]
public sealed class MonthGridBuilderTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset October = new(2026, 10, 1, 0, 0, 0, Offset);
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, Offset);

    [Test]
    public void Build_WhenAMonthIsGiven_ShouldReturnSixWeeksStartingOnMonday()
    {
        var cells = MonthGridBuilder.Build(October, [], Now);

        cells.Should().HaveCount(MonthGridBuilder.CellCount);
        cells[0].Date.DayOfWeek.Should().Be(DayOfWeek.Monday);
        cells[0].Date.Date.Should().Be(new DateTime(2026, 9, 28), "the grid starts on the Monday before October");
        cells[^1].Date.Date.Should().Be(new DateTime(2026, 11, 8));
    }

    [Test]
    public void Build_WhenTheMonthHasLeadingDays_ShouldMarkOnlyItsOwnDays()
    {
        var cells = MonthGridBuilder.Build(October, [], Now);

        cells.Count(c => c.IsInMonth).Should().Be(31);
        cells.Take(3).Should().OnlyContain(c => !c.IsInMonth);
        cells.Single(c => c.Date.Date == new DateTime(2026, 10, 1)).IsInMonth.Should().BeTrue();
    }

    [Test]
    public void Build_WhenADayHasSessions_ShouldSumTheirClippedDurations()
    {
        var cells = MonthGridBuilder.Build(
            October,
            [
                Session(October.AddDays(4).AddHours(9), 60, "Report"),
                Session(October.AddDays(4).AddHours(14), 30, "Review"),
            ],
            Now);

        var cell = cells.Single(c => c.Date.Date == new DateTime(2026, 10, 5));
        cell.TotalText.Should().Be("Σ 1:30");
        cell.TotalSeconds.Should().Be(5400);
    }

    [Test]
    public void Build_WhenADayHasManyTasks_ShouldShowThreeChipsAndTheOverflowCount()
    {
        var day = October.AddDays(4);
        var sessions = new[]
        {
            Session(day.AddHours(8), 30, "A"),
            Session(day.AddHours(9), 30, "B"),
            Session(day.AddHours(10), 30, "C"),
            Session(day.AddHours(11), 30, "D"),
        };

        var cell = MonthGridBuilder.Build(October, sessions, Now)
            .Single(c => c.Date.Date == day.Date);

        cell.Chips.Should().Equal("A", "B", "C");
        cell.HiddenChipCount.Should().Be(1);
        cell.HasMore.Should().BeTrue();
        cell.MoreText.Should().Be("+1 more");
    }

    [Test]
    public void Build_WhenTheSameTaskIsBookedTwice_ShouldShowOneChip()
    {
        var day = October.AddDays(4);

        var cell = MonthGridBuilder.Build(
            October,
            [Session(day.AddHours(9), 30, "Report"), Session(day.AddHours(14), 30, "report")],
            Now).Single(c => c.Date.Date == day.Date);

        cell.Chips.Should().ContainSingle().Which.Should().Be("Report");
    }

    [Test]
    public void Build_WhenTheDayIsToday_ShouldMarkIt()
    {
        var cells = MonthGridBuilder.Build(October, [], Now);

        cells.Single(c => c.IsToday).Date.Date.Should().Be(Now.Date);
        cells.Should().ContainSingle(c => c.IsToday);
    }

    [Test]
    public void Build_WhenASessionCrossesMidnight_ShouldSplitItsTotalBetweenTheDays()
    {
        var cells = MonthGridBuilder.Build(
            October,
            [Session(October.AddDays(4).AddHours(23), 120, "Late")],
            Now);

        cells.Single(c => c.Date.Date == new DateTime(2026, 10, 5)).TotalSeconds.Should().Be(3600);
        cells.Single(c => c.Date.Date == new DateTime(2026, 10, 6)).TotalSeconds.Should().Be(3600);
    }

    [Test]
    public void Build_WhenASessionIsRunning_ShouldCountUntilNow()
    {
        var day = October.AddDays(4);
        var running = Session(day.AddHours(11), 0, "Running") with { End = default };
        var now = day.AddHours(12);

        var cell = MonthGridBuilder.Build(October, [running], now).Single(c => c.Date.Date == day.Date);

        cell.TotalSeconds.Should().Be(3600);
    }

    private static TrackedSession Session(DateTimeOffset start, int minutes, string task)
    {
        var duration = TimeSpan.FromMinutes(minutes);
        var result = new TrackedSession
        {
            Task = task,
            Start = start,
            End = start.Add(duration),
            Duration = duration.ToString(@"hh\:mm\:ss"),
            DurationSeconds = duration.TotalSeconds,
        };
        return result;
    }
}
