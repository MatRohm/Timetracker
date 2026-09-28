using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Tests.Unit.ViewModels;

/// <summary>
/// The history-editing use cases: deleting rows, replacing a task's sessions and
/// committing an inline text edit. The editor persists through a delegate and
/// reports the outcome; it never touches status or events.
/// </summary>
[TestFixture]
public sealed class EntryEditorTests
{
    [Test]
    public void BuildDeleteSummary_WhenOneRow_ShouldNameTheTaskAndItsSessionCount()
    {
        var row = Row(Entry("Report", 9), Entry("Report", 14));

        var summary = EntryEditor.BuildDeleteSummary([row]);

        summary.Should().Be("\"Report\" (all 2 sessions)");
    }

    [Test]
    public void BuildDeleteSummary_WhenSeveralRows_ShouldCountTasksAndSessions()
    {
        var rows = new[] { Row(Entry("Report", 9)), Row(Entry("Meeting", 11), Entry("Meeting", 14)) };

        var summary = EntryEditor.BuildDeleteSummary(rows);

        summary.Should().Be("2 tasks (3 sessions)");
    }

    [Test]
    public async Task DeleteAsync_WhenConfirmed_ShouldPersistOnlyTheRemainingSessions()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, (_, _) => { });
        var sessions = new[] { Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13) };
        var row = Row(Entry("Meeting", 11));

        var result = await editor.DeleteAsync([row], sessions, _ => true);

        result.Status.Should().Be(EntryEditStatus.Saved);
        saved!.Select(e => e.Task).Should().BeEquivalentTo("Report", "Review");
    }

    [Test]
    public async Task DeleteAsync_WhenUserDeclines_ShouldNotPersist()
    {
        var saved = false;
        var editor = new EntryEditor(_ => { saved = true; return Task.CompletedTask; }, (_, _) => { });
        var row = Row(Entry("Report", 9));

        var result = await editor.DeleteAsync([row], [Entry("Report", 9)], _ => false);

        result.Status.Should().Be(EntryEditStatus.Declined);
        saved.Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_WhenThereAreNoRows_ShouldDeclineWithoutPersisting()
    {
        var saved = false;
        var editor = new EntryEditor(_ => { saved = true; return Task.CompletedTask; }, (_, _) => { });

        var result = await editor.DeleteAsync([], [], _ => true);

        result.Status.Should().Be(EntryEditStatus.Declined);
        saved.Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_WhenTheSaveFails_ShouldReportFailureWithTheError()
    {
        var logged = false;
        var editor = new EntryEditor(
            _ => Task.FromException(new IOException("disk full")),
            (_, _) => logged = true);
        var row = Row(Entry("Report", 9));

        var result = await editor.DeleteAsync([row], [Entry("Report", 9)], _ => true);

        result.Status.Should().Be(EntryEditStatus.Failed);
        result.Error.Should().BeOfType<IOException>();
        logged.Should().BeTrue("the failure is logged for the error log");
    }

    [Test]
    public async Task ReplaceSessionsAsync_WhenCalled_ShouldReplaceOnlyThatTasksSessions()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, (_, _) => { });
        var sessions = new[] { Entry("Report", 9), Entry("Meeting", 11) };
        var replacement = new SessionEditRow(Entry("Report", 15));

        var result = await editor.ReplaceSessionsAsync("Report", [replacement], sessions);

        result.Status.Should().Be(EntryEditStatus.Saved);
        result.Summary.Should().Be("Report");
        saved!.Select(e => e.Start.Hour).Should().BeEquivalentTo(new[] { 15, 11 });
    }

    [Test]
    public async Task UpdateTextAsync_WhenTheTaskNameIsEmpty_ShouldRejectAsInvalid()
    {
        var saved = false;
        var editor = new EntryEditor(_ => { saved = true; return Task.CompletedTask; }, (_, _) => { });
        var row = Row(Entry("Report", 9));

        var result = await editor.UpdateTextAsync(row, "   ", "", [Entry("Report", 9)]);

        result.Status.Should().Be(EntryEditStatus.InvalidTaskName);
        saved.Should().BeFalse();
    }

    [Test]
    public async Task UpdateTextAsync_WhenNothingChanged_ShouldRestoreTheCommittedTextWithoutSaving()
    {
        var saved = false;
        var editor = new EntryEditor(_ => { saved = true; return Task.CompletedTask; }, (_, _) => { });
        var row = Row(Entry("Report", 9));
        row.BookingElement = "staged but uncommitted";

        var result = await editor.UpdateTextAsync(row, "Report", "", [Entry("Report", 9)]);

        result.Status.Should().Be(EntryEditStatus.Unchanged);
        saved.Should().BeFalse();
        row.CommittedBookingElement.Should().Be("", "the committed snapshot is unchanged");
    }

    [Test]
    public async Task UpdateTextAsync_WhenChanged_ShouldPersistAndCommitTheRow()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, (_, _) => { });
        var sessions = new[] { Entry("Report", 9, "quarterly") };
        var row = Row(sessions);

        var result = await editor.UpdateTextAsync(row, "Edited", "new element", sessions);

        result.Status.Should().Be(EntryEditStatus.Saved);
        result.Summary.Should().Be("Edited");
        row.CommittedTask.Should().Be("Edited");
        row.CommittedBookingElement.Should().Be("new element");
        saved!.Single().Task.Should().Be("Edited");
    }

    [Test]
    public async Task UpdateTextAsync_WhenTheSaveFails_ShouldReportFailure()
    {
        var editor = new EntryEditor(
            _ => Task.FromException(new IOException("disk full")),
            (_, _) => { });
        var sessions = new[] { Entry("Report", 9) };
        var row = Row(sessions);

        var result = await editor.UpdateTextAsync(row, "Edited", "", sessions);

        result.Status.Should().Be(EntryEditStatus.Failed);
        result.Error.Should().BeOfType<IOException>();
    }

    private static EntryRow Row(params TrackerEntry[] sessions) => new(sessions);

    private static TrackerEntry Entry(string task, int hour, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(2026, 9, 19, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 19, hour, 30, 0, TimeSpan.FromHours(2)),
        Duration = "00:30:00",
        DurationSeconds = 1800,
    };
}
