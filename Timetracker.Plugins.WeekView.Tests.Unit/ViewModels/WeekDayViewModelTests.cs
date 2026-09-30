using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Services;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Tests.Unit.ViewModels;

[TestFixture]
public sealed class WeekDayViewModelTests
{
    [Test]
    public void Header_WhenUpdated_ShouldShowTheShortWeekdayAndDate()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));

        day.Update(date, []);

        day.Header.Should().Be("Mon 21.09.");
    }

    [Test]
    public void TotalText_WhenADayHasSessions_ShouldSumThem()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));

        day.Update(date, [Session(date, 9, 60, "A"), Session(date, 14, 30, "B")]);

        day.TotalText.Should().Be("Σ 1:30");
    }

    [Test]
    public void TotalText_WhenADayHasNoSessions_ShouldBeEmpty()
    {
        var day = new WeekDayViewModel();

        day.Update(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)), []);

        day.TotalText.Should().BeEmpty();
    }

    [Test]
    public void Update_WhenAnEntryHasNoBookingElement_ShouldPutItInTheNoneGroup()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));

        day.Update(date, [Session(date, 9, 45, "Adhoc")]);

        day.Groups.Should().ContainSingle();
        day.Groups[0].Name.Should().Be(WeekDayViewModel.NoBookingElementLabel);
        day.Groups[0].Entries.Single().Task.Should().Be("Adhoc");
    }

    [Test]
    public void Update_WhenTasksDifferOnlyByCase_ShouldMergeThemKeepingTheFirstSpelling()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));

        day.Update(date,
        [
            Session(date, 9, 60, "Report", "Project X"),
            Session(date, 14, 15, "report", "Project X"),
        ]);

        var entry = day.Groups.Single().Entries.Single();
        entry.Task.Should().Be("Report");
        entry.TotalText.Should().Be("1:15");
    }

    [Test]
    public void Update_WhenGroupsAndTasksHaveDifferentStarts_ShouldOrderThemByEarliestStart()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));

        day.Update(date,
        [
            Session(date, 14, 30, "Late", "Project Y"),
            Session(date, 9, 30, "Early", "Project X"),
            Session(date, 11, 30, "Mid", "Project X"),
        ]);

        day.Groups.Select(g => g.Name).Should().Equal("Project X", "Project Y");
        day.Groups[0].Entries.Select(e => e.Task).Should().Equal("Early", "Mid");
    }

    [Test]
    public void ToggleCommand_WhenExecuted_ShouldToggleTheDay()
    {
        var day = new WeekDayViewModel();

        day.ToggleCommand.Execute(null);
        day.IsExpanded.Should().BeTrue();

        day.ToggleCommand.Execute(null);
        day.IsExpanded.Should().BeFalse();
    }

    [Test]
    public void CollapseAll_WhenTheDayAndItsGroupsAreExpanded_ShouldCollapseThem()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        day.Update(date, [Session(date, 9, 60, "Report", "Project X")]);
        day.IsExpanded = true;
        day.Groups.Single().IsExpanded = true;

        day.CollapseAll();

        day.IsExpanded.Should().BeFalse();
        day.Groups.Single().IsExpanded.Should().BeFalse();
    }

    [Test]
    public void Update_WhenAGroupStaysPresent_ShouldKeepItsExpansion()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        day.Update(date, [Session(date, 9, 60, "Report", "Project X")]);
        day.Groups.Single().IsExpanded = true;

        day.Update(date,
        [
            Session(date, 9, 60, "Report", "Project X"),
            Session(date, 14, 30, "Review", "Project X"),
        ]);

        day.Groups.Single().IsExpanded.Should().BeTrue();
    }

    [Test]
    public void UntrackedText_WhenActiveTimeExceedsTheBookedTimeByTheThreshold_ShouldWarnWithTheDifference()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        day.Update(date, [Session(date, 9, 60, "A"), Session(date, 14, 30, "B")]);

        day.ActiveTime = TimeSpan.FromMinutes(155);

        day.UntrackedText.Should().Be("⚠ 1:05 untracked");
        day.IsUntrackedWarning.Should().BeTrue();
    }

    [Test]
    public void UntrackedText_WhenTheGapIsBelowTheWarningThreshold_ShouldShowItWithoutWarning()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        day.Update(date, [Session(date, 9, 90, "A")]);

        day.ActiveTime = TimeSpan.FromMinutes(100);

        day.UntrackedText.Should().Be("0:10 untracked");
        day.IsUntrackedWarning.Should().BeFalse();
    }

    [Test]
    public void UntrackedText_WhenTheBookingsCoverTheActiveTime_ShouldBeEmpty()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        day.Update(date, [Session(date, 9, 90, "A")]);

        day.ActiveTime = TimeSpan.FromMinutes(60);

        day.UntrackedText.Should().BeEmpty("booking more than the PC was active is not a gap");
        day.IsUntrackedWarning.Should().BeFalse();
    }

    [Test]
    public void UntrackedText_WhenNoActivityWasRecorded_ShouldBeEmpty()
    {
        var day = new WeekDayViewModel();

        day.Update(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)), []);

        day.UntrackedText.Should().BeEmpty("without the activity monitor there is nothing to compare");
    }

    [Test]
    public void UntrackedText_WhenTheSessionsChangeAfterTheActiveTimeWasSet_ShouldRecalculate()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        day.Update(date, []);
        day.ActiveTime = TimeSpan.FromMinutes(60);

        day.Update(date, [Session(date, 9, 50, "A")]);

        day.UntrackedText.Should().Be("0:10 untracked");
    }

    [Test]
    public void RoundingLines_WhenATaskIsRoundedUp_ShouldDescribeTotalsAndTheChangedSession()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        var original = Session(date, 10, 50, "Report");
        var updated = Session(date, 10, 60, "Report");

        day.SetRounding(new RoundingPlan(
            [new RoundedTask("Report", TimeSpan.FromMinutes(50), TimeSpan.FromHours(1), [new SessionChange(original, updated)])],
            []));

        day.RoundingLines.Should().Equal("Report 0:50 → 1:00: 10:00–10:50 → 10:00–11:00");
    }

    [Test]
    public void RoundingLines_WhenASessionIsRemoved_ShouldSaySo()
    {
        var day = new WeekDayViewModel();
        var date = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2));
        var tail = Session(date, 11, 10, "Report");

        day.SetRounding(new RoundingPlan(
            [new RoundedTask("Report", TimeSpan.FromMinutes(70), TimeSpan.FromHours(1), [new SessionChange(tail, null)])],
            []));

        day.RoundingLines.Single().Should().EndWith("11:00–11:10 removed");
    }

    [Test]
    public void RoundingSkippedLines_WhenATaskCannotBeRounded_ShouldGiveTheReason()
    {
        var day = new WeekDayViewModel();

        day.SetRounding(new RoundingPlan([], [new SkippedTask("Report", TimeSpan.FromMinutes(50))]));

        day.RoundingSkippedLines.Should().Equal("Report 0:50 (no free time next to its last session)");
        day.CanRound.Should().BeTrue("the user should learn why nothing can be rounded");
    }

    private static TrackedSession Session(DateTimeOffset date, int startHour, int minutes, string task, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = date.AddHours(startHour),
        End = date.AddHours(startHour).AddMinutes(minutes),
        Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
        DurationSeconds = minutes * 60.0,
    };
}
