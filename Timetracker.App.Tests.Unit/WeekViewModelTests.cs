using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

[TestFixture]
public sealed class WeekViewModelTests
{
    [Test]
    public void Days_WhenTheViewModelIsCreated_ShouldStartOnTheCurrentWeekWithMondayFirst()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        week.Days.Should().HaveCount(7);
        week.Days[0].Date.Date.Should().Be(monday, "weeks start on Monday");
        week.Days[6].Date.Date.Should().Be(monday.AddDays(6), "last column is Sunday");
        week.Days.Single(d => d.IsToday).Date.Date.Should().Be(today);
    }

    [Test]
    public void UpdateSessions_WhenSessionsSpanDays_ShouldShowThemOnlyInTheirOwnDayColumns()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var wednesday = monday.AddDays(2);

        week.UpdateSessions([Session(wednesday, 10, 45, "Review")]);

        week.Days[2].EntriesText.Should().Contain("Review");
        week.Days.Where(d => d.Date != wednesday.Date)
            .Should().OnlyContain(d => d.EntriesText.Length == 0);
    }

    [Test]
    public void UpdateSessions_WhenGroupingByBookingElement_ShouldMergeEntriesAcrossTasks()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 11, 30, "Meeting", "Project X"),
            Session(today, 13, 45, "Review", "Project Y"),
        ]);

        var lines = week.Days.Single(d => d.IsToday).EntriesText.Split(Environment.NewLine);

        // Default mode: grouped by booking element; the label is the element name.
        lines.Should().HaveCount(2, "both Project X entries merge into one line");
        lines[0].Should().Be("Project X (1:30)");
        lines[1].Should().Be("Project Y (0:45)");
    }

    [Test]
    public void GroupByBookingElement_WhenSetToFalse_ShouldShowTaskNamesAgain()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "Report", "Project X"),
            Session(today, 12, 30, "Meeting", "Project X"),
        ]);

        week.GroupByBookingElement = false;

        var lines = week.Days.Single(d => d.IsToday).EntriesText.Split(Environment.NewLine);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("Report (1:30)");
        lines[1].Should().Be("Meeting (0:30)");
    }

    [Test]
    public void UpdateSessions_WhenAnEntryHasNoBookingElement_ShouldLabelItWhenGroupingByElement()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions([Session(today, 9, 60, "Adhoc")]);

        week.Days.Single(d => d.IsToday).EntriesText.Should().Contain("Adhoc (1:00)");
    }

    [Test]
    public void UpdateSessions_WhenGroupingByTask_ShouldMergeEntriesWithTheSameTask()
    {
        var week = new WeekViewModel
        {
            GroupByBookingElement = false,
        };
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report"),
            Session(today, 14, 15, "report"),
        ]);

        week.Days.Single(d => d.IsToday).EntriesText.Should().Contain("Report (1:15)");
    }

    [Test]
    public void UpdateSessions_WhenGroupingByBookingElement_ShouldCopyTaskNamesIntoEachLine()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "Review", "Project X"),
        ]);

        var group = week.Days.Single(d => d.IsToday).Groups.Single();

        group.Label.Should().Be("Project X (1:30)");
        group.CopyText.Split(Environment.NewLine).Should().Equal("Report", "Review");
    }

    [Test]
    public void UpdateSessions_WhenGroupingByBookingElement_ShouldGiveEachDayOneLinePerElement()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 12, 30, "Meeting", "Project Y"),
            Session(today, 14, 30, "Report", "Project X"),
        ]);

        var groups = week.Days.Single(d => d.IsToday).Groups;

        groups.Should().HaveCount(2, "one line per booking element");
        groups[0].Label.Should().Be("Project X (1:30)");
        groups[0].CopyText.Should().Be("Report" + Environment.NewLine + "Report");
        groups[1].Label.Should().Be("Project Y (0:30)");
        groups[1].CopyText.Should().Be("Meeting");
    }

    [Test]
    public void UpdateSessions_WhenGroupingByTask_ShouldCopyTaskNamesIntoEachLine()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;
        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 15, "report", "Project X"),
        ]);

        week.GroupByBookingElement = false;

        var group = week.Days.Single(d => d.IsToday).Groups.Single();
        group.Label.Should().Be("Report (1:15)");
        group.CopyText.Split(Environment.NewLine).Should().Equal("Report", "report");
    }

    [Test]
    public void UpdateSessions_WhenADayHasNoBookings_ShouldHaveNoLines()
    {
        var week = new WeekViewModel();

        week.Days.Should().OnlyContain(d => d.Groups.Count == 0);
    }

    [Test]
    public void WeekTotalText_WhenSessionsSpanTheWeek_ShouldSumThem()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));

        week.UpdateSessions(
        [
            Session(monday, 9, 60, "A"),
            Session(monday.AddDays(1), 9, 30, "B"),
            Session(monday.AddDays(2), 9, 45, "C"),
        ]);

        week.WeekTotalText.Should().Be("Σ 2:15");
    }

    [Test]
    public void PreviousWeekCommand_WhenNavigating_ShouldMoveAFullWeekAndKeepTheSelection()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var lastWeek = monday.AddDays(-2);

        week.UpdateSessions([Session(lastWeek, 8, 120, "Old stuff")]);
        week.PreviousWeekCommand.Execute(null);

        week.Days.Single(d => d.Date.Date == lastWeek).EntriesText.Should().Contain("Old stuff");
        week.Days.Should().OnlyContain(d => d.EntriesText.Contains("Old stuff") || d.EntriesText.Length == 0);

        week.NextWeekCommand.Execute(null);
        week.Days[0].Date.Date.Should().Be(monday, "back to the current week");

        week.NextWeekCommand.Execute(null);
        week.Days[0].Date.Date.Should().Be(monday.AddDays(7));
        week.Days.Should().OnlyContain(d => d.EntriesText.Length == 0, "the future week has no data");

        week.CurrentWeekCommand.Execute(null);
        week.Days[0].Date.Date.Should().Be(monday, "current week jumps back");
    }

    private static TrackerEntry Session(DateTime date, int startHour, int minutes, string task, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(date.AddHours(startHour), DateTimeOffset.Now.Offset),
        End = new DateTimeOffset(date.AddHours(startHour).AddMinutes(minutes), DateTimeOffset.Now.Offset),
        Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
        DurationSeconds = minutes * 60.0,
    };
}
