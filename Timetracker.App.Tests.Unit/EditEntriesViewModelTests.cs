using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

/// <summary>
/// Behavior of the per-item session editor dialog: it shows every session of one
/// tracking item, lets each session's start/end be edited (duration is derived)
/// and lets individual sessions be deleted.
/// </summary>
public sealed class EditEntriesViewModelTests
{
    [Test]
    public void EditEntriesViewModel_WhenItemHasSessions_ShouldListThemChronologically()
    {
        var item = Item("Report",
            Session("Report", day: 18, hour: 14, minutes: 30),
            Session("Report", day: 18, hour: 9, minutes: 60),
            Session("Report", day: 19, hour: 8, minutes: 45));

        var editor = new EditEntriesViewModel(item);

        editor.Sessions.Select(s => s.StartText).Should().BeInAscendingOrder();
        editor.Sessions.Should().HaveCount(3);
        editor.TaskName.Should().Be("Report");
    }

    [Test]
    public void EditEntriesViewModel_WhenStartAndEndAreEdited_ShouldRecalculateTheDuration()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();

        row.StartText = "2026-09-18 08:45"; // 15 minutes earlier
        row.EndText = "2026-09-18 10:15";   // 45 minutes later than 09:30

        // 30 min originally, minus 15 moved earlier, plus 45 moved later = 90 min.
        row.DurationText.Should().Be("01:30:00");
        row.DurationSeconds.Should().Be(5400);
    }

    [Test]
    public void EditEntriesViewModel_WhenStartDateIsEdited_ShouldMoveTheWholeSession()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();

        row.StartText = "2026-09-19 09:00";
        row.EndText = "2026-09-19 09:30";

        row.StartText.Should().Be("2026-09-19 09:00");
        row.IsValid.Should().BeTrue();
    }

    [Test]
    public void EditEntriesViewModel_WhenEndIsBeforeStart_ShouldClampDurationToZeroAndRejectOnSave()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();

        row.EndText = "2026-09-18 08:45"; // the session starts at 09:00, so this is before it

        row.DurationSeconds.Should().Be(0);
        editor.HasErrors.Should().BeTrue("an end before the start cannot be saved");
        editor.CanSave.Should().BeFalse();
    }

    [Test]
    public void EditEntriesViewModel_WhenTimeIsUnparsable_ShouldRejectOnSave()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();

        row.StartText = "not a date";

        row.IsValid.Should().BeFalse();
        editor.HasErrors.Should().BeTrue();
        editor.CanSave.Should().BeFalse();
        editor.Save().Should().BeFalse();
    }

    [Test]
    public void EditEntriesViewModel_WhenTimeTextIsEdited_ShouldMoveStagedStartAndShowItBack()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();

        row.StartText = "2026-09-18 08:15";

        row.Start.Hour.Should().Be(8);
        row.Start.Minute.Should().Be(15);
        row.StartText.Should().Be("2026-09-18 08:15");
        row.IsValid.Should().BeTrue();
    }

    [Test]
    public void EditEntriesViewModel_WhenSessionHasStoredOffset_ShouldKeepItInsteadOfMachineTimezone()
    {
        // The session was recorded at +10:00; editing must not reinterpret the typed
        // wall clock in whatever offset the machine running the tests happens to have.
        var offset = TimeSpan.FromHours(10);
        var session = new TrackerEntry
        {
            Task = "Report",
            Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, offset),
            End = new DateTimeOffset(2026, 9, 18, 9, 30, 0, offset),
            Duration = "00:30:00",
            DurationSeconds = 1800,
        };
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();

        row.StartText = "2026-09-18 08:15";

        row.Start.Offset.Should().Be(offset);
        row.Start.Hour.Should().Be(8);
        row.Start.Minute.Should().Be(15);
        row.StartText.Should().Be("2026-09-18 08:15");
    }

    [Test]
    public void EditEntriesViewModel_WhenSaved_ShouldApplyEditedTimesToUnderlyingSession()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();
        var newStart = session.Start.AddHours(2);
        var newEnd = session.End.AddHours(2);

        row.SetStart(newStart);
        row.SetEnd(newEnd);

        editor.CanSave.Should().BeTrue();
        editor.Save();

        session.Start.Should().Be(newStart);
        session.End.Should().Be(newEnd);
        session.DurationSeconds.Should().Be(1800);
    }

    [Test]
    public void EditEntriesViewModel_WhenTimeTextIsStale_ShouldIgnoreItOnSave()
    {
        // The text boxes are refreshed on commit; a programmatic SetStart/SetEnd must
        // win over whatever text happens to be staged.
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();
        var newStart = session.Start.AddHours(1);
        var newEnd = session.End.AddHours(1);

        row.SetStart(newStart);
        row.SetEnd(newEnd);
        var saved = editor.Save();

        saved.Should().BeTrue();
        session.Start.Should().Be(newStart);
        session.End.Should().Be(newEnd);
    }

    [Test]
    public void EditEntriesViewModel_WhenSessionIsInvalid_ShouldRejectSaveAndKeepUnderlyingEntryUntouched()
    {
        var session = Session("Report", day: 18, hour: 9, minutes: 30);
        var editor = new EditEntriesViewModel(Item("Report", session));
        var row = editor.Sessions.Single();
        var originalStart = session.Start;
        row.SetStart(session.End);
        row.SetEnd(session.Start); // inverted

        var saved = editor.Save();

        saved.Should().BeFalse();
        session.Start.Should().Be(originalStart, "nothing is written while a row is invalid");
    }

    [Test]
    public void EditEntriesViewModel_WhenSessionIsDeleted_ShouldRemoveOnlyThatSession()
    {
        var first = Session("Report", day: 18, hour: 9, minutes: 30);
        var second = Session("Report", day: 18, hour: 14, minutes: 60);
        var editor = new EditEntriesViewModel(Item("Report", first, second));

        editor.Sessions.Should().HaveCount(2);
        var removed = editor.RemoveSession(editor.Sessions.Single(s => s.Start == second.Start));

        removed.Should().BeTrue();
        editor.Sessions.Should().ContainSingle().Which.Start.Should().Be(first.Start);
        editor.SessionsModified.Should().BeTrue();
    }

    [Test]
    public void EditEntriesViewModel_WhenTotalsAreEdited_ShouldReflectThemInSummary()
    {
        var editor = new EditEntriesViewModel(Item("Report",
            Session("Report", day: 18, hour: 9, minutes: 30),
            Session("Report", day: 18, hour: 14, minutes: 60)));

        // 30 + 60 minutes = 1:30:00 across two sessions.
        editor.SummaryText.Should().Contain("2");
        editor.SummaryText.Should().Contain("01:30:00");
    }

    [Test]
    public void EditEntriesViewModel_WhenItemHasNoSessions_ShouldReportItAsEmpty()
    {
        var editor = new EditEntriesViewModel(new EntryRow([]));

        editor.Sessions.Should().BeEmpty();
        editor.CanSave.Should().BeFalse();
    }
    private static EntryRow Item(string task, params TrackerEntry[] sessions) =>
        new([.. sessions]);

    /// <summary>A session starting at <paramref name="hour"/>:00 that lasts <paramref name="minutes"/>.</summary>
    private static TrackerEntry Session(string task, int day, int hour, int minutes)
    {
        var start = new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.FromHours(2));
        var end = start.AddMinutes(minutes);
        return new TrackerEntry
        {
            Task = task,
            Start = start,
            End = end,
            Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
            DurationSeconds = minutes * 60.0,
        };
    }
}
