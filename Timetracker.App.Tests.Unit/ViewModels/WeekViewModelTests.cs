using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Tests.Unit.ViewModels;

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
    public void Days_WhenTheWeekIsShown_ShouldBeAllCollapsed()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;
        week.UpdateSessions([Session(today, 9, 60, "Report", "Project X")]);

        week.Days.Should().OnlyContain(d => !d.IsExpanded);
        week.Days.Single(d => d.IsToday).Groups.Should().OnlyContain(g => !g.IsExpanded);
    }

    [Test]
    public void UpdateSessions_WhenSessionsSpanDays_ShouldGroupThemUnderTheirOwnDay()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var wednesday = monday.AddDays(2);

        week.UpdateSessions([Session(wednesday, 10, 45, "Review")]);

        week.Days[2].Groups.Single().Entries.Single().Task.Should().Be("Review");
        week.Days.Where(d => d.Date != wednesday.Date)
            .Should().OnlyContain(d => d.Groups.Count == 0);
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

        var groups = week.Days.Single(d => d.IsToday).Groups;

        groups.Select(g => g.Name).Should().Equal("Project X", "Project Y");
        groups[0].TotalText.Should().Be("1:30", "both Project X entries merge under one element");
        groups[1].TotalText.Should().Be("0:45");
    }

    [Test]
    public void UpdateSessions_WhenAnEntryHasNoBookingElement_ShouldPutItInTheNoneGroup()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Adhoc"),
            Session(today, 11, 30, "Report", "Project X"),
        ]);

        var groups = week.Days.Single(d => d.IsToday).Groups;

        groups.Should().Contain(g => g.Name == WeekDayViewModel.NoBookingElementLabel);
        groups.Single(g => g.Name == WeekDayViewModel.NoBookingElementLabel)
            .Entries.Single().Task.Should().Be("Adhoc");
    }

    [Test]
    public void UpdateSessions_WhenTheSameTaskIsWorkedTwice_ShouldMergeItIntoOneEntry()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "report", "Project X"),
        ]);

        var entries = week.Days.Single(d => d.IsToday).Groups.Single().Entries;

        entries.Should().ContainSingle("the same task merges, case-insensitively");
        entries[0].Task.Should().Be("Report", "the first-seen spelling is kept");
        entries[0].TotalText.Should().Be("1:30");
    }

    [Test]
    public void UpdateSessions_WhenGroupingByElement_ShouldCopyTheTaskNamesOfTheGroup()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "Review", "Project X"),
        ]);

        var group = week.Days.Single(d => d.IsToday).Groups.Single();

        group.CopyText.Split(Environment.NewLine).Should().Equal("Report", "Review");
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
    public void UpdateSessions_WhenAnActiveTimeSourceIsWired_ShouldShowEachDaysUntrackedTime()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var mondayDate = DateOnly.FromDateTime(monday);
        week.DayActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(120) : TimeSpan.Zero;

        week.UpdateSessions([Session(monday, 9, 60, "A")]);

        week.Days[0].ActiveTime.Should().Be(TimeSpan.FromMinutes(120));
        week.Days[0].UntrackedText.Should().Be("⚠ 1:00 untracked");
        week.Days.Skip(1).Should().OnlyContain(d => d.UntrackedText.Length == 0,
            "days without recorded activity show no untracked time");
    }

    [Test]
    public void PreviousWeekCommand_WhenNavigating_ShouldMoveAFullWeekAndKeepTheSelection()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var lastWeek = monday.AddDays(-2);

        week.UpdateSessions([Session(lastWeek, 8, 120, "Old stuff")]);
        week.PreviousWeekCommand.Execute(null);

        week.Days.Single(d => d.Date.Date == lastWeek).Groups.Should().NotBeEmpty();

        week.NextWeekCommand.Execute(null);
        week.Days[0].Date.Date.Should().Be(monday, "back to the current week");

        week.NextWeekCommand.Execute(null);
        week.Days[0].Date.Date.Should().Be(monday.AddDays(7));
        week.Days.Should().OnlyContain(d => d.Groups.Count == 0, "the future week has no data");

        week.CurrentWeekCommand.Execute(null);
        week.Days[0].Date.Date.Should().Be(monday, "current week jumps back");
    }

    [Test]
    public void PreviousWeekCommand_WhenTheWeekChanges_ShouldCollapseEveryNodeAgain()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;
        week.UpdateSessions([Session(today, 9, 60, "Report", "Project X")]);
        week.Days.Single(d => d.IsToday).IsExpanded = true;
        week.Days.Single(d => d.IsToday).Groups.Single().IsExpanded = true;

        week.PreviousWeekCommand.Execute(null);

        week.Days.Should().OnlyContain(d => !d.IsExpanded, "a week change collapses the tree");
        week.Days.Should().OnlyContain(d => d.Groups.All(g => !g.IsExpanded));
    }

    [Test]
    public void UpdateSessions_WhenDataRefreshes_ShouldKeepTheUsersExpansion()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;
        week.UpdateSessions([Session(today, 9, 60, "Report", "Project X")]);
        var day = week.Days.Single(d => d.IsToday);
        day.IsExpanded = true;
        day.Groups.Single().IsExpanded = true;

        // A live refresh (e.g. a session being saved) must not reset the tree.
        week.UpdateSessions(
        [
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "Review", "Project X"),
        ]);

        var refreshed = week.Days.Single(d => d.IsToday);
        refreshed.IsExpanded.Should().BeTrue("the day stays expanded across a refresh");
        refreshed.Groups.Single().IsExpanded.Should().BeTrue("the element stays expanded too");
        refreshed.Groups.Single().Entries.Should().HaveCount(2);
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
