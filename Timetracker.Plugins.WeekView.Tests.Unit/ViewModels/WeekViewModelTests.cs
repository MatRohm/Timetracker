using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Tests.Unit.ViewModels;

[TestFixture]
public sealed class WeekViewModelTests
{
    [Test]
    public void Days_WhenTheViewModelIsCreated_ShouldStartOnTheCurrentWeekWithMondayFirst()
    {
        var week = Create(out _, out _);
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
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;
        sessions.SetSessions(Session(today, 9, 60, "Report", "Project X"));

        week.Days.Should().OnlyContain(d => !d.IsExpanded);
        week.Days.Single(d => d.IsToday).Groups.Should().OnlyContain(g => !g.IsExpanded);
    }

    [Test]
    public void Days_WhenSessionsSpanDays_ShouldGroupThemUnderTheirOwnDay()
    {
        var week = Create(out var sessions, out _);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var wednesday = monday.AddDays(2);

        sessions.SetSessions(Session(wednesday, 10, 45, "Review"));

        week.Days[2].Groups.Single().Entries.Single().Task.Should().Be("Review");
        week.Days.Where(d => d.Date != wednesday.Date)
            .Should().OnlyContain(d => d.Groups.Count == 0);
    }

    [Test]
    public void Days_WhenGroupingByBookingElement_ShouldMergeEntriesAcrossTasks()
    {
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;

        sessions.SetSessions(
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 11, 30, "Meeting", "Project X"),
            Session(today, 13, 45, "Review", "Project Y"));

        var groups = week.Days.Single(d => d.IsToday).Groups;

        groups.Select(g => g.Name).Should().Equal("Project X", "Project Y");
        groups[0].TotalText.Should().Be("1:30", "both Project X entries merge under one element");
        groups[1].TotalText.Should().Be("0:45");
    }

    [Test]
    public void Days_WhenAnEntryHasNoBookingElement_ShouldPutItInTheNoneGroup()
    {
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;

        sessions.SetSessions(
            Session(today, 9, 60, "Adhoc"),
            Session(today, 11, 30, "Report", "Project X"));

        var groups = week.Days.Single(d => d.IsToday).Groups;

        groups.Should().Contain(g => g.Name == WeekDayViewModel.NoBookingElementLabel);
        groups.Single(g => g.Name == WeekDayViewModel.NoBookingElementLabel)
            .Entries.Single().Task.Should().Be("Adhoc");
    }

    [Test]
    public void Days_WhenTheSameTaskIsWorkedTwice_ShouldMergeItIntoOneEntry()
    {
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;

        sessions.SetSessions(
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "report", "Project X"));

        var entries = week.Days.Single(d => d.IsToday).Groups.Single().Entries;

        entries.Should().ContainSingle("the same task merges, case-insensitively");
        entries[0].Task.Should().Be("Report", "the first-seen spelling is kept");
        entries[0].TotalText.Should().Be("1:30");
    }

    [Test]
    public void Days_WhenGroupingByElement_ShouldCopyTheTaskNamesOfTheGroup()
    {
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;

        sessions.SetSessions(
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "Review", "Project X"));

        var group = week.Days.Single(d => d.IsToday).Groups.Single();

        group.CopyText.Split(Environment.NewLine).Should().Equal("Report", "Review");
    }

    [Test]
    public void WeekTotalText_WhenSessionsSpanTheWeek_ShouldSumThem()
    {
        var week = Create(out var sessions, out _);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));

        sessions.SetSessions(
            Session(monday, 9, 60, "A"),
            Session(monday.AddDays(1), 9, 30, "B"),
            Session(monday.AddDays(2), 9, 45, "C"));

        week.WeekTotalText.Should().Be("Σ 2:15");
    }

    [Test]
    public void Days_WhenActiveSpansAreWired_ShouldListEachDaysGaps()
    {
        var week = Create(out var sessions, out var activity);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var mondayDate = DateOnly.FromDateTime(monday);
        var offset = DateTimeOffset.Now.Offset;
        activity.ActiveSpans = day => day == mondayDate
            ? [new ActiveSpan(new DateTimeOffset(monday.AddHours(9), offset), new DateTimeOffset(monday.AddHours(12), offset))]
            : [];

        sessions.SetSessions(Session(monday, 9, 60, "A"));

        week.Days[0].Gaps.Select(g => g.Text).Should().Equal("⚠ 10:00–12:00 untracked (2:00)");
        week.Days.Skip(1).Should().OnlyContain(d => d.Gaps.Count == 0);
    }

    [Test]
    public void CreateDistributeDay_WhenTheDayHasUntrackedTime_ShouldPrefillTheProportionalShares()
    {
        var week = Create(out var sessions, out var activity);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var mondayDate = DateOnly.FromDateTime(monday);
        activity.ActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(150) : TimeSpan.Zero;
        sessions.SetSessions(Session(monday, 9, 60, "A"));

        var day = week.Days[0];
        var dialog = week.CreateDistributeDay(day);

        dialog.Rows.Should().ContainSingle(r => r.Task == "A");
        dialog.Rows[0].ShareText.Should().Be("+1:30", "the whole untracked time goes to the single task");
        dialog.MissingText.Should().Be("1:30 untracked");
        dialog.Plan.Changes.Should().ContainSingle().Which.Updated!.End.Hour.Should().Be(11, "A's 09:00–10:00 grows to 09:00–11:30");
    }

    [Test]
    public async Task ApplyDistributeAsync_WhenTheUserAccepts_ShouldApplyThePlansChanges()
    {
        var week = Create(out var sessions, out var activity);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var mondayDate = DateOnly.FromDateTime(monday);
        activity.ActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(150) : TimeSpan.Zero;
        sessions.SetSessions(Session(monday, 9, 60, "A"));
        IReadOnlyList<SessionChange>? applied = null;
        sessions.ApplyChanges = changes => { applied = changes; return Task.FromResult(true); };

        var day = week.Days[0];
        var dialog = week.CreateDistributeDay(day);
        var result = await week.ApplyDistributeAsync(day, dialog.Plan);

        result.Should().BeTrue();
        applied.Should().BeSameAs(dialog.Plan.Changes);
        week.Status.Should().Be(WeekStatus.Success);
        week.StatusText.Should().StartWith("✓ Distributed");
    }

    [Test]
    public async Task ApplyDistributeAsync_WhenThePlanIsEmpty_ShouldReportAndApplyNothing()
    {
        var week = Create(out var sessions, out _);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        sessions.SetSessions(Session(monday, 9, 60, "A"));
        var applied = false;
        sessions.ApplyChanges = _ => { applied = true; return Task.FromResult(true); };

        var day = week.Days[0];
        day.CanDistribute.Should().BeFalse("the day has no untracked time");

        var result = await week.ApplyDistributeAsync(day, day.Distribution);

        result.Should().BeFalse();
        applied.Should().BeFalse();
        week.Status.Should().Be(WeekStatus.Info);
        week.StatusText.Should().Contain("Nothing could be distributed");
    }

    [Test]
    public async Task RoundDayAsync_WhenTheUserConfirms_ShouldApplyTheDaysRoundingPlan()
    {
        var week = Create(out var sessions, out _);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        sessions.SetSessions(Session(monday, 9, 50, "A"));
        IReadOnlyList<SessionChange>? applied = null;
        sessions.ApplyChanges = changes => { applied = changes; return Task.FromResult(true); };
        IReadOnlyList<string>? shown = null;

        var result = await week.RoundDayAsync(week.Days[0], lines => { shown = lines; return Task.FromResult(true); });

        result.Should().BeTrue();
        week.Days[0].CanRound.Should().BeTrue();
        applied.Should().BeSameAs(week.Days[0].Rounding.Changes);
        shown.Should().Equal("A 0:50 → 1:00: 09:00–09:50 → 09:00–10:00");
        week.Status.Should().Be(WeekStatus.Success);
    }

    [Test]
    public async Task RoundDayAsync_WhenEveryTaskIsOnAHalfHour_ShouldNotAskOrApply()
    {
        var week = Create(out var sessions, out _);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        sessions.SetSessions(Session(monday, 9, 90, "A"));
        var asked = false;
        sessions.ApplyChanges = _ => Task.FromResult(true);

        var result = await week.RoundDayAsync(week.Days[0], _ => { asked = true; return Task.FromResult(true); });

        week.Days[0].CanRound.Should().BeFalse();
        result.Should().BeFalse();
        asked.Should().BeFalse();
    }

    [Test]
    public void Days_WhenATimerRuns_ShouldKeepItsTimeOutOfTheGaps()
    {
        var week = Create(out var sessions, out var activity);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var offset = DateTimeOffset.Now.Offset;
        activity.ActiveSpans = _ =>
            [new ActiveSpan(new DateTimeOffset(monday.AddHours(9), offset), new DateTimeOffset(monday.AddHours(12), offset))];
        sessions.SetSessions();

        sessions.RunningSince = new DateTimeOffset(monday.AddHours(11), offset);
        sessions.SetSessions();

        week.Days[0].Gaps.Select(g => g.TimeText).Should().Equal("09:00–11:00");
    }

    [Test]
    public void Days_WhenAnActiveTimeSourceIsWired_ShouldShowEachDaysUntrackedTime()
    {
        var week = Create(out var sessions, out var activity);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var mondayDate = DateOnly.FromDateTime(monday);
        activity.ActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(120) : TimeSpan.Zero;

        sessions.SetSessions(Session(monday, 9, 60, "A"));

        week.Days[0].ActiveTime.Should().Be(TimeSpan.FromMinutes(120));
        week.Days[0].UntrackedText.Should().Be("⚠ 1:00 untracked");
        week.Days.Skip(1).Should().OnlyContain(d => d.UntrackedText.Length == 0,
            "days without recorded activity show no untracked time");
    }

    [Test]
    public void PreviousWeekCommand_WhenNavigating_ShouldMoveAFullWeekAndKeepTheSelection()
    {
        var week = Create(out var sessions, out _);
        var monday = DateTimeOffset.Now.Date.AddDays(-(((int)DateTimeOffset.Now.DayOfWeek + 6) % 7));
        var lastWeek = monday.AddDays(-2);

        sessions.SetSessions(Session(lastWeek, 8, 120, "Old stuff"));
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
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;
        sessions.SetSessions(Session(today, 9, 60, "Report", "Project X"));
        week.Days.Single(d => d.IsToday).IsExpanded = true;
        week.Days.Single(d => d.IsToday).Groups.Single().IsExpanded = true;

        week.PreviousWeekCommand.Execute(null);

        week.Days.Should().OnlyContain(d => !d.IsExpanded, "a week change collapses the tree");
        week.Days.Should().OnlyContain(d => d.Groups.All(g => !g.IsExpanded));
    }

    [Test]
    public void Days_WhenDataRefreshes_ShouldKeepTheUsersExpansion()
    {
        var week = Create(out var sessions, out _);
        var today = DateTimeOffset.Now.Date;
        sessions.SetSessions(Session(today, 9, 60, "Report", "Project X"));
        var day = week.Days.Single(d => d.IsToday);
        day.IsExpanded = true;
        day.Groups.Single().IsExpanded = true;

        // A live refresh (e.g. a session being saved) must not reset the tree.
        sessions.SetSessions(
            Session(today, 9, 60, "Report", "Project X"),
            Session(today, 14, 30, "Review", "Project X"));

        var refreshed = week.Days.Single(d => d.IsToday);
        refreshed.IsExpanded.Should().BeTrue("the day stays expanded across a refresh");
        refreshed.Groups.Single().IsExpanded.Should().BeTrue("the element stays expanded too");
        refreshed.Groups.Single().Entries.Should().HaveCount(2);
    }

    private static WeekViewModel Create(out FakeTrackedSessions sessions, out FakeDayActivitySource activity)
    {
        sessions = new FakeTrackedSessions();
        activity = new FakeDayActivitySource();
        var result = new WeekViewModel(sessions, sessions, [], [activity]);
        return result;
    }

    private static TrackedSession Session(DateTime date, int startHour, int minutes, string task, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(date.AddHours(startHour), DateTimeOffset.Now.Offset),
        End = new DateTimeOffset(date.AddHours(startHour).AddMinutes(minutes), DateTimeOffset.Now.Offset),
        Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
        DurationSeconds = minutes * 60.0,
    };
}
