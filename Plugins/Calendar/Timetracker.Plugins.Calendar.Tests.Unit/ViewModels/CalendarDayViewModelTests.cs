using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Tests.Unit.ViewModels;

[TestFixture]
public sealed class CalendarDayViewModelTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Test]
    public void Header_WhenUpdated_ShouldShowTheShortWeekdayAndDate()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);

        day.Update(date, []);

        day.Header.Should().Be("Mon 21.09.");
    }

    [Test]
    public void TotalText_WhenADayHasSessions_ShouldSumThem()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);

        day.Update(date, [Block(date, 9, 60, "A"), Block(date, 14, 30, "B")]);

        day.TotalText.Should().Be("Σ 1:30");
    }

    [Test]
    public void TotalText_WhenADayHasNoSessions_ShouldBeEmpty()
    {
        var day = new CalendarDayViewModel();

        day.Update(new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset), []);

        day.TotalText.Should().BeEmpty();
    }

    [Test]
    public void Update_WhenSessionsAreGiven_ShouldKeepThemOrderedByStart()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);

        day.Update(date,
        [
            Block(date, 14, 30, "Late", "Project Y"),
            Block(date, 9, 30, "Early", "Project X"),
            Block(date, 11, 30, "Mid", "Project X"),
        ]);

        day.Sessions.Select(s => s.Task).Should().Equal("Early", "Mid", "Late");
        day.Sessions[0].BookingElement.Should().Be("Project X");
    }

    [Test]
    public void Update_WhenABlockIsGiven_ShouldExposeItsClippedTimesAndCopyText()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);

        day.Update(date, [Block(date, 9, 45, "Adhoc")]);

        var block = day.Sessions.Should().ContainSingle().Subject;
        block.TimeText.Should().Be("09:00–09:45");
        block.DurationText.Should().Be("0:45");
        block.CopyText.Should().Be("Adhoc");
        block.ToolTip.Should().NotContain("Project", "an empty element is left out");
    }

    [Test]
    public void SetActiveSpans_WhenSpansArriveOutOfOrder_ShouldOrderThem()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);
        day.Update(date, []);

        day.SetActiveSpans(
        [
            new(date.AddHours(11), date.AddHours(12)),
            new(date.AddHours(8), date.AddHours(9)),
        ]);

        day.ActiveSpans.Should().HaveCount(2);
        day.ActiveSpans[0].Start.Should().Be(date.AddHours(8));
    }

    [Test]
    public void UntrackedText_WhenActiveTimeExceedsTheBookedTimeByTheThreshold_ShouldWarnWithTheDifference()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);
        day.Update(date, [Block(date, 9, 60, "A"), Block(date, 14, 30, "B")]);

        day.ActiveTime = TimeSpan.FromMinutes(155);

        day.UntrackedText.Should().Be("⚠ 1:05 untracked");
        day.IsUntrackedWarning.Should().BeTrue();
    }

    [Test]
    public void UntrackedText_WhenTheGapIsBelowTheWarningThreshold_ShouldShowItWithoutWarning()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);
        day.Update(date, [Block(date, 9, 90, "A")]);

        day.ActiveTime = TimeSpan.FromMinutes(100);

        day.UntrackedText.Should().Be("0:10 untracked");
        day.IsUntrackedWarning.Should().BeFalse();
    }

    [Test]
    public void UntrackedText_WhenTheBookingsCoverTheActiveTime_ShouldBeEmpty()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);
        day.Update(date, [Block(date, 9, 90, "A")]);

        day.ActiveTime = TimeSpan.FromMinutes(60);

        day.UntrackedText.Should().BeEmpty("booking more than the PC was active is not a gap");
        day.IsUntrackedWarning.Should().BeFalse();
    }

    [Test]
    public void UntrackedText_WhenNoActivityWasRecorded_ShouldBeEmpty()
    {
        var day = new CalendarDayViewModel();

        day.Update(new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset), []);

        day.UntrackedText.Should().BeEmpty("without the activity monitor there is nothing to compare");
    }

    [Test]
    public void UntrackedText_WhenTheSessionsChangeAfterTheActiveTimeWasSet_ShouldRecalculate()
    {
        var day = new CalendarDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, Offset);
        day.Update(date, []);
        day.ActiveTime = TimeSpan.FromMinutes(60);

        day.Update(date, [Block(date, 9, 50, "A")]);

        day.UntrackedText.Should().Be("0:10 untracked");
    }

    private static CalendarSessionViewModel Block(
        DateTimeOffset date,
        int startHour,
        int minutes,
        string task,
        string bookingElement = "")
    {
        var start = date.AddHours(startHour);
        var result = new CalendarSessionViewModel(task, bookingElement, start, start.AddMinutes(minutes), false);
        return result;
    }
}
