using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.ViewModels;

namespace Timetracker.Tests.Unit;

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

    private static TrackerEntry Session(DateTimeOffset date, int startHour, int minutes, string task, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = date.AddHours(startHour),
        End = date.AddHours(startHour).AddMinutes(minutes),
        Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
        DurationSeconds = minutes * 60.0,
    };
}
