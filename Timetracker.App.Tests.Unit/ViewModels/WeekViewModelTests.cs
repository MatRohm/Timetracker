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
    public void UpdateSessions_WhenActiveSpansAreWired_ShouldListEachDaysGaps()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var mondayDate = DateOnly.FromDateTime(monday);
        var offset = DateTimeOffset.Now.Offset;
        week.DayActiveSpans = day => day == mondayDate
            ? [new TimeRange(new DateTimeOffset(monday.AddHours(9), offset), new DateTimeOffset(monday.AddHours(12), offset))]
            : [];

        week.UpdateSessions([Session(monday, 9, 60, "A")]);

        week.Days[0].Gaps.Select(g => g.Text).Should().Equal("⚠ 10:00–12:00 untracked (2:00)");
        week.Days.Skip(1).Should().OnlyContain(d => d.Gaps.Count == 0);
    }

    [Test]
    public async Task DistributeAsync_WhenTheUserConfirms_ShouldApplyTheGapsPlannedChanges()
    {
        var (week, gap) = WeekWithOneGap(sessionBordersTheGap: true);
        IReadOnlyList<SessionChange>? applied = null;
        week.ApplyChanges = changes => { applied = changes; return Task.FromResult(true); };
        IReadOnlyList<string>? shown = null;

        var result = await week.DistributeAsync(gap, lines => { shown = lines; return Task.FromResult(true); });

        result.Should().BeTrue();
        applied.Should().BeSameAs(gap.Distribution);
        shown.Should().ContainSingle().Which.Should().Be("A 09:00–10:00 → 09:00–12:00");
        week.Status.Should().Be(WeekStatus.Success);
    }

    [Test]
    public async Task DistributeAsync_WhenTheUserDeclines_ShouldApplyNothing()
    {
        var (week, gap) = WeekWithOneGap(sessionBordersTheGap: true);
        var applied = false;
        week.ApplyChanges = _ => { applied = true; return Task.FromResult(true); };

        var result = await week.DistributeAsync(gap, _ => Task.FromResult(false));

        result.Should().BeFalse();
        applied.Should().BeFalse();
    }

    [Test]
    public async Task DistributeAsync_WhenNoSessionBordersTheGap_ShouldNotAskOrApply()
    {
        var (week, gap) = WeekWithOneGap(sessionBordersTheGap: false);
        var asked = false;
        week.ApplyChanges = _ => Task.FromResult(true);

        var result = await week.DistributeAsync(gap, _ => { asked = true; return Task.FromResult(true); });

        gap.CanDistribute.Should().BeFalse();
        result.Should().BeFalse();
        asked.Should().BeFalse();
    }

    /// <summary>
    /// Monday active 09:00–12:00 with one session; the gap it leaves either borders
    /// the session (session 09:00–10:00, gap 10:00–12:00) or not (session 08:00–08:30
    /// outside the activity, gap 09:00–12:00).
    /// </summary>
    private static (WeekViewModel Week, WeekGapViewModel Gap) WeekWithOneGap(bool sessionBordersTheGap)
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var offset = DateTimeOffset.Now.Offset;
        week.DayActiveSpans = day => day == DateOnly.FromDateTime(monday)
            ? [new TimeRange(new DateTimeOffset(monday.AddHours(9), offset), new DateTimeOffset(monday.AddHours(12), offset))]
            : [];
        week.UpdateSessions([sessionBordersTheGap ? Session(monday, 9, 60, "A") : Session(monday, 8, 30, "A")]);
        var result = (week, week.Days[0].Gaps.Single());
        return result;
    }

    [Test]
    public void RunningSince_WhenATimerRuns_ShouldKeepItsTimeOutOfTheGaps()
    {
        var week = new WeekViewModel();
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var offset = DateTimeOffset.Now.Offset;
        week.DayActiveSpans = _ =>
            [new TimeRange(new DateTimeOffset(monday.AddHours(9), offset), new DateTimeOffset(monday.AddHours(12), offset))];
        week.UpdateSessions([]);

        week.RunningSince = new DateTimeOffset(monday.AddHours(11), offset);

        week.Days[0].Gaps.Select(g => g.TimeText).Should().Equal("09:00–11:00");
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
