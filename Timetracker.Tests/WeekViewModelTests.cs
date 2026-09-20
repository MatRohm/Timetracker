using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.ViewModels;

namespace Timetracker.Tests;

public sealed class WeekViewModelTests
{
    [Test]
    public void Starts_on_the_current_week_with_monday_first()
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
    public void Day_columns_show_only_their_own_sessions()
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
    public void Group_by_booking_element_merges_entries_across_tasks()
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
    public void Switching_to_task_grouping_shows_task_names_again()
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
    public void Entries_without_booking_element_are_labeled_when_grouping_by_element()
    {
        var week = new WeekViewModel();
        var today = DateTimeOffset.Now.Date;

        week.UpdateSessions([Session(today, 9, 60, "Adhoc")]);

        week.Days.Single(d => d.IsToday).EntriesText.Should().Contain("Adhoc (1:00)");
    }

    [Test]
    public void Same_task_entries_merge_in_task_grouping_mode()
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
    public void Week_total_sums_all_sessions_of_the_week()
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
    public void Navigation_moves_a_full_week_and_keeps_selection()
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
