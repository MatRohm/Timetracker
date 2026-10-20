using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.Tests.Unit.ViewModels;

[TestFixture]
public sealed class CalendarViewModelTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    /// <summary>Monday 21 September 2026, noon.</summary>
    private static readonly DateTimeOffset MondayNoon = new(2026, 9, 21, 12, 0, 0, Offset);

    [Test]
    public void Days_WhenTheViewModelIsCreated_ShouldStartOnTheCurrentWeekWithMondayFirst()
    {
        var calendar = Create(out _, out _, out _);

        calendar.Days.Should().HaveCount(7);
        calendar.Days[0].Date.Date.Should().Be(new DateTime(2026, 9, 21), "weeks start on Monday");
        calendar.Days[6].Date.Date.Should().Be(new DateTime(2026, 9, 27), "last column is Sunday");
        calendar.Days.Single(d => d.IsToday).Date.Date.Should().Be(MondayNoon.Date);
    }

    [Test]
    public void Days_WhenSessionsSpanDays_ShouldPutEachBlockUnderItsOwnDay()
    {
        var calendar = Create(out var sessions, out _, out _);
        var wednesday = MondayNoon.Date.AddDays(2);

        sessions.SetSessions(Session(new DateTimeOffset(wednesday, Offset).AddHours(10), 45, "Review"));

        calendar.Days[2].Sessions.Single().Task.Should().Be("Review");
        calendar.Days.Where(d => d.Date.Date != wednesday)
            .Should().OnlyContain(d => d.Sessions.Count == 0);
    }

    [Test]
    public void Days_WhenASessionCrossesMidnight_ShouldSplitItAcrossBothDays()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);

        sessions.SetSessions(Session(monday.AddHours(23), 120, "Late"));

        calendar.Days[0].Sessions.Single().TimeText.Should().Be("23:00–00:00");
        calendar.Days[0].Sessions.Single().DurationText.Should().Be("1:00");
        calendar.Days[1].Sessions.Single().TimeText.Should().Be("00:00–01:00");
        calendar.Days[1].Sessions.Single().DurationText.Should().Be("1:00");
    }

    [Test]
    public void Days_WhenASessionStartedTheDayBefore_ShouldShowItsRemainder()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);

        sessions.SetSessions(Session(monday.AddDays(-1).AddHours(23), 120, "Late"));

        calendar.Days[0].Sessions.Single().TimeText.Should().Be("00:00–01:00");
        calendar.Days[0].Sessions.Single().Task.Should().Be("Late");
    }

    [Test]
    public void Days_WhenATimerRuns_ShouldExtendTheBlockToNow()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        var running = Session(monday.AddHours(11), 0, "Running") with { End = default };
        sessions.RunningSince = monday.AddHours(11);

        sessions.SetSessions(running);

        var block = calendar.Days[0].Sessions.Should().ContainSingle().Subject;
        block.IsRunning.Should().BeTrue();
        block.DurationText.Should().Be("1:00");
    }

    [Test]
    public void Axis_WhenADayHasSessions_ShouldFitTheirTimes()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);

        sessions.SetSessions(Session(monday.AddHours(9), 480, "All day"));

        calendar.Axis.Start.Should().Be(TimeSpan.FromHours(9));
        calendar.Axis.End.Should().Be(TimeSpan.FromHours(17));
    }

    [Test]
    public void Axis_WhenTodayIsVisible_ShouldIncludeNow()
    {
        var calendar = Create(out var sessions, out _, out _, now: () => new DateTimeOffset(2026, 9, 21, 20, 0, 0, Offset));
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);

        sessions.SetSessions(Session(monday.AddHours(9), 60, "Report"));

        calendar.Axis.Start.Should().Be(TimeSpan.FromHours(9));
        calendar.Axis.End.Should().Be(TimeSpan.FromHours(20));
    }

    [Test]
    public void NowLineMinutes_WhenTodayIsVisible_ShouldPointAtNow()
    {
        var calendar = Create(out var sessions, out _, out _, now: () => new DateTimeOffset(2026, 9, 21, 12, 0, 0, Offset));
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);

        sessions.SetSessions(Session(monday.AddHours(9), 480, "All day"));

        calendar.NowLineMinutes.Should().Be(TimeSpan.FromHours(3).TotalMinutes);
    }

    [Test]
    public void NowLineMinutes_WhenTodayIsAWeekendInWorkweekMode_ShouldBeNull()
    {
        var saturday = new DateTimeOffset(2026, 9, 26, 12, 0, 0, Offset);
        var calendar = Create(out _, out _, out _, now: () => saturday);

        calendar.Mode.Should().Be(CalendarViewMode.Workweek);
        calendar.NowLineMinutes.Should().BeNull("the weekend is not a visible column");
    }

    [Test]
    public async Task RefreshNow_WhenTheClockMoves_ShouldMoveTheNowLine()
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, Offset);
        var calendar = Create(out var sessions, out _, out _, now: () => now);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        sessions.SetSessions(Session(monday.AddHours(9), 480, "All day"));
        var before = calendar.NowLineMinutes;

        now = now.AddMinutes(30);
        calendar.RefreshNow();

        calendar.NowLineMinutes.Should().Be(before + 30);
    }

    [Test]
    public void Mode_WhenNothingIsStored_ShouldBeWorkweek()
    {
        var calendar = Create(out _, out _, out _);

        calendar.Mode.Should().Be(CalendarViewMode.Workweek);
    }

    [Test]
    public void Mode_WhenAValueIsStored_ShouldRestoreIt()
    {
        var calendar = Create(out _, out _, out _, storedMode: nameof(CalendarViewMode.Month));

        calendar.Mode.Should().Be(CalendarViewMode.Month);
    }

    [Test]
    public void Mode_WhenTheStoredValueIsInvalid_ShouldFallBackToWorkweek()
    {
        var calendar = Create(out _, out _, out _, storedMode: "Every other Tuesday");

        calendar.Mode.Should().Be(CalendarViewMode.Workweek);
    }

    [Test]
    public async Task ChangeModeAsync_WhenAModeIsSelected_ShouldSwitchAndPersistIt()
    {
        var calendar = Create(out _, out _, out var options);

        await calendar.ChangeModeAsync(CalendarViewMode.Month);

        calendar.Mode.Should().Be(CalendarViewMode.Month);
        options.GetValue(CalendarOptions.ViewModeKey).Should().Be(nameof(CalendarViewMode.Month));
        options.Writes.Should().Contain(CalendarOptions.ViewModeKey);
    }

    [Test]
    public async Task ChangeModeAsync_WhenSavingFails_ShouldKeepTheModeAndShowAnError()
    {
        var calendar = Create(out _, out _, out var options);
        options.SaveThrows = true;

        await calendar.ChangeModeAsync(CalendarViewMode.Month);

        calendar.Mode.Should().Be(CalendarViewMode.Month);
        calendar.Status.Should().Be(CalendarStatusKind.Error);
        calendar.StatusText.Should().Be("Could not save the calendar view mode.");
    }

    [Test]
    public void PreviousCommand_WhenInWeekMode_ShouldMoveSevenDays()
    {
        var calendar = Create(out _, out _, out _);

        calendar.PreviousCommand.Execute(null);

        calendar.Days[0].Date.Date.Should().Be(new DateTime(2026, 9, 14));
    }

    [Test]
    public void NextCommand_WhenInWeekMode_ShouldMoveSevenDays()
    {
        var calendar = Create(out _, out _, out _);

        calendar.NextCommand.Execute(null);

        calendar.Days[0].Date.Date.Should().Be(new DateTime(2026, 9, 28));
    }

    [Test]
    public async Task PreviousCommand_WhenInMonthMode_ShouldMoveOneMonth()
    {
        var calendar = Create(out _, out _, out _);
        await calendar.ChangeModeAsync(CalendarViewMode.Month);

        calendar.PreviousCommand.Execute(null);

        calendar.CalendarTitle.Should().Be("August 2026");
        calendar.MonthDays[0].Date.Date.Should().Be(new DateTime(2026, 7, 27), "the grid starts on a Monday");
    }

    [Test]
    public async Task CurrentCommand_WhenInMonthMode_ShouldReturnToTheCurrentMonth()
    {
        var calendar = Create(out _, out _, out _);
        await calendar.ChangeModeAsync(CalendarViewMode.Month);
        calendar.NextCommand.Execute(null);
        calendar.NextCommand.Execute(null);

        calendar.CurrentCommand.Execute(null);

        calendar.CalendarTitle.Should().Be("September 2026");
    }

    [Test]
    public async Task MonthDays_WhenInMonthMode_ShouldCoverTheCurrentMonth()
    {
        var calendar = Create(out _, out _, out _);
        await calendar.ChangeModeAsync(CalendarViewMode.Month);

        calendar.MonthDays.Should().HaveCount(42);
        calendar.MonthDays.Count(d => d.IsInMonth).Should().Be(30);
    }

    [Test]
    public async Task ChangeModeAsync_WhenTheCurrentWeekStraddlesAMonth_ShouldOpenTheMonthsOwnMonth()
    {
        // Thursday 1 October 2026 belongs to the week starting Monday 28 September.
        var thursday = new DateTimeOffset(2026, 10, 1, 12, 0, 0, Offset);
        var calendar = Create(out _, out _, out _, now: () => thursday);

        await calendar.ChangeModeAsync(CalendarViewMode.Month);

        calendar.CalendarTitle.Should().Be("October 2026", "the week mostly belongs to October");
        calendar.MonthDays.Count(d => d.IsInMonth).Should().Be(31);
    }

    [Test]
    public async Task ShowWeekAsync_WhenAMonthEdgeCellIsClicked_ShouldOpenThatCellsWeek()
    {
        var october = new DateTimeOffset(2026, 10, 19, 12, 0, 0, Offset);
        var calendar = Create(out _, out _, out _, storedMode: nameof(CalendarViewMode.Month), now: () => october);
        var novemberFirst = new DateTimeOffset(2026, 11, 1, 12, 0, 0, Offset);

        await calendar.ShowWeekAsync(novemberFirst);

        calendar.Mode.Should().Be(CalendarViewMode.Week);
        calendar.Days[0].Date.Date.Should().Be(new DateTime(2026, 10, 26),
            "the clicked trailing cell's week is kept");
    }

    [Test]
    public void CalendarTotalText_WhenInWorkweekMode_ShouldLeaveOutTheWeekend()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);

        sessions.SetSessions(
            Session(monday.AddHours(9), 60, "Monday"),
            Session(monday.AddDays(5).AddHours(9), 60, "Saturday"));

        calendar.CalendarTotalText.Should().Be("Σ 1:00");
    }

    [Test]
    public async Task CalendarTotalText_WhenInWeekMode_ShouldIncludeTheWeekend()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        sessions.SetSessions(
            Session(monday.AddHours(9), 60, "Monday"),
            Session(monday.AddDays(5).AddHours(9), 60, "Saturday"));

        await calendar.ChangeModeAsync(CalendarViewMode.Week);

        calendar.CalendarTotalText.Should().Be("Σ 2:00");
    }

    [Test]
    public void CalendarTotalText_WhenInMonthMode_ShouldSumTheMonth()
    {
        var october = new DateTimeOffset(2026, 10, 19, 12, 0, 0, Offset);
        var calendar = Create(
            out var sessions,
            out _,
            out _,
            storedMode: nameof(CalendarViewMode.Month),
            now: () => october);

        sessions.SetSessions(
            Session(new DateTimeOffset(2026, 10, 5, 9, 0, 0, Offset), 60, "October"),
            Session(new DateTimeOffset(2026, 11, 2, 9, 0, 0, Offset), 60, "November"));

        calendar.CalendarTitle.Should().Be("October 2026");
        calendar.CalendarTotalText.Should().Be("Σ 1:00");
    }

    [Test]
    public async Task ShowWeekAsync_WhenADateIsGiven_ShouldShowThatWeekInWeekMode()
    {
        var calendar = Create(out _, out _, out _);
        var date = new DateTimeOffset(2026, 10, 14, 12, 0, 0, Offset);

        await calendar.ShowWeekAsync(date);

        calendar.Mode.Should().Be(CalendarViewMode.Week);
        calendar.Days[0].Date.Date.Should().Be(new DateTime(2026, 10, 12), "the Monday of that week");
    }

    [Test]
    public void Days_WhenActiveSpansAreWired_ShouldListEachDaysGaps()
    {
        var calendar = Create(out var sessions, out var activity, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        var mondayDate = DateOnly.FromDateTime(monday.Date);
        activity.ActiveSpans = day => day == mondayDate
            ? [new ActiveSpan(monday.AddHours(9), monday.AddHours(12))]
            : [];

        sessions.SetSessions(Session(monday.AddHours(9), 60, "A"));

        calendar.Days[0].Gaps.Select(g => g.Text).Should().Equal("⚠ 10:00–12:00 untracked (2:00)");
        calendar.Days.Skip(1).Should().OnlyContain(d => d.Gaps.Count == 0);
    }

    [Test]
    public void Days_WhenActiveSpansAreWired_ShouldExposeThemForTheGreenStrip()
    {
        var calendar = Create(out var sessions, out var activity, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        var mondayDate = DateOnly.FromDateTime(monday.Date);
        activity.ActiveSpans = day => day == mondayDate
            ? [new ActiveSpan(monday.AddHours(9), monday.AddHours(10))]
            : [];

        sessions.SetSessions(Session(monday.AddHours(9), 60, "A"));

        calendar.Days[0].ActiveSpans.Should().ContainSingle();
        calendar.Days[0].ActiveSpans[0].Start.Should().Be(monday.AddHours(9));
    }

    [Test]
    public void CreateDistributeDay_WhenTheDayHasUntrackedTime_ShouldPrefillTheProportionalShares()
    {
        var calendar = Create(out var sessions, out var activity, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        var mondayDate = DateOnly.FromDateTime(monday.Date);
        activity.ActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(150) : TimeSpan.Zero;
        sessions.SetSessions(Session(monday.AddHours(9), 60, "A"));

        var day = calendar.Days[0];
        var dialog = calendar.CreateDistributeDay(day);

        dialog.Rows.Should().ContainSingle(r => r.Task == "A");
        dialog.Rows[0].ShareText.Should().Be("+1:30", "the whole untracked time goes to the single task");
        dialog.MissingText.Should().Be("1:30 untracked");
        dialog.Plan.Changes.Should().ContainSingle().Which.Updated!.End.Hour.Should().Be(11, "A's 09:00–10:00 grows to 09:00–11:30");
    }

    [Test]
    public async Task ApplyDistributeAsync_WhenTheUserAccepts_ShouldApplyThePlansChanges()
    {
        var calendar = Create(out var sessions, out var activity, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        var mondayDate = DateOnly.FromDateTime(monday.Date);
        activity.ActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(150) : TimeSpan.Zero;
        sessions.SetSessions(Session(monday.AddHours(9), 60, "A"));
        IReadOnlyList<SessionChange>? applied = null;
        sessions.ApplyChanges = changes => { applied = changes; return Task.FromResult(true); };

        var day = calendar.Days[0];
        var dialog = calendar.CreateDistributeDay(day);
        var result = await calendar.ApplyDistributeAsync(day, dialog.Plan);

        result.Should().BeTrue();
        applied.Should().BeSameAs(dialog.Plan.Changes);
        calendar.Status.Should().Be(CalendarStatusKind.Success);
        calendar.StatusText.Should().StartWith("✓ Distributed");
    }

    [Test]
    public async Task ApplyDistributeAsync_WhenThePlanIsEmpty_ShouldReportAndApplyNothing()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        sessions.SetSessions(Session(monday.AddHours(9), 60, "A"));
        var applied = false;
        sessions.ApplyChanges = _ => { applied = true; return Task.FromResult(true); };

        var day = calendar.Days[0];
        day.CanDistribute.Should().BeFalse("the day has no untracked time");

        var result = await calendar.ApplyDistributeAsync(day, day.Distribution);

        result.Should().BeFalse();
        applied.Should().BeFalse();
        calendar.Status.Should().Be(CalendarStatusKind.Info);
        calendar.StatusText.Should().Contain("Nothing could be distributed");
    }

    [Test]
    public void Days_WhenATimerRuns_ShouldKeepItsTimeOutOfTheGaps()
    {
        var calendar = Create(out var sessions, out var activity, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        activity.ActiveSpans = _ => [new ActiveSpan(monday.AddHours(9), monday.AddHours(12))];
        sessions.SetSessions();

        sessions.RunningSince = monday.AddHours(11);
        sessions.SetSessions();

        calendar.Days[0].Gaps.Select(g => g.TimeText).Should().Equal("09:00–11:00");
    }

    [Test]
    public void Days_WhenAnActiveTimeSourceIsWired_ShouldShowEachDaysUntrackedTime()
    {
        var calendar = Create(out var sessions, out var activity, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        var mondayDate = DateOnly.FromDateTime(monday.Date);
        activity.ActiveTime = day => day == mondayDate ? TimeSpan.FromMinutes(120) : TimeSpan.Zero;

        sessions.SetSessions(Session(monday.AddHours(9), 60, "A"));

        calendar.Days[0].ActiveTime.Should().Be(TimeSpan.FromMinutes(120));
        calendar.Days[0].UntrackedText.Should().Be("⚠ 1:00 untracked");
        calendar.Days.Skip(1).Should().OnlyContain(d => d.UntrackedText.Length == 0,
            "days without recorded activity show no untracked time");
    }

    [Test]
    public void Days_WhenDataRefreshes_ShouldUpdateTheBlocks()
    {
        var calendar = Create(out var sessions, out _, out _);
        var monday = new DateTimeOffset(MondayNoon.Date, Offset);
        sessions.SetSessions(Session(monday.AddHours(9), 60, "Report"));

        sessions.SetSessions(
            Session(monday.AddHours(9), 60, "Report"),
            Session(monday.AddHours(14), 30, "Review"));

        calendar.Days[0].Sessions.Should().HaveCount(2);
    }

    private static CalendarViewModel Create(
        out FakeTrackedSessions sessions,
        out FakeDayActivitySource activity,
        out FakeOptions options,
        string? storedMode = null,
        Func<DateTimeOffset>? now = null)
    {
        sessions = new FakeTrackedSessions();
        activity = new FakeDayActivitySource();
        options = new FakeOptions();
        if (storedMode is not null)
        {
            options.Set(CalendarOptions.ViewModeKey, storedMode);
        }

        var result = new CalendarViewModel(
            sessions,
            sessions,
            [],
            [activity],
            options,
            options,
            now ?? (() => MondayNoon));
        return result;
    }

    private static TrackedSession Session(DateTimeOffset start, int minutes, string task, string bookingElement = "")
    {
        var duration = TimeSpan.FromMinutes(minutes);
        var result = new TrackedSession
        {
            Task = task,
            BookingElement = bookingElement,
            Start = start,
            End = start.Add(duration),
            Duration = duration.ToString(@"hh\:mm\:ss"),
            DurationSeconds = duration.TotalSeconds,
        };
        return result;
    }
}
