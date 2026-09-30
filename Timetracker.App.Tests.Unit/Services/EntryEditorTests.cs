using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

/// <summary>
/// The session-log editing use cases: removing sessions, replacing a task's
/// sessions, renaming them and applying planned changes. The editor persists
/// through a delegate and reports the outcome; it never touches rows, status or
/// events.
/// </summary>
[TestFixture]
public sealed class EntryEditorTests
{
    [Test]
    public async Task DeleteAsync_WhenSessionsAreRemoved_ShouldPersistOnlyTheRemainingOnes()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, NullLogger.Instance);
        var sessions = new[] { Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13) };

        var result = await editor.DeleteAsync([Entry("Meeting", 11)], sessions);

        result.Status.Should().Be(EntryEditStatus.Saved);
        saved!.Select(e => e.Task).Should().BeEquivalentTo("Report", "Review");
    }

    [Test]
    public async Task DeleteAsync_WhenNothingIsToBeRemoved_ShouldDeclineWithoutPersisting()
    {
        var saved = false;
        var editor = new EntryEditor(_ => { saved = true; return Task.CompletedTask; }, NullLogger.Instance);

        var result = await editor.DeleteAsync([], [Entry("Report", 9)]);

        result.Status.Should().Be(EntryEditStatus.Declined);
        saved.Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_WhenTheSaveFails_ShouldReportFailureWithTheError()
    {
        var logger = new FakeLogger();
        var editor = new EntryEditor(
            _ => Task.FromException(new IOException("disk full")),
            logger);

        var result = await editor.DeleteAsync([Entry("Report", 9)], [Entry("Report", 9)]);

        result.Status.Should().Be(EntryEditStatus.Failed);
        result.Error.Should().BeOfType<IOException>();
        logger.LatestRecord.Level.Should().Be(LogLevel.Error, "the failure is logged for the error log");
        logger.LatestRecord.Exception.Should().BeOfType<IOException>();
    }

    [Test]
    public async Task ReplaceSessionsAsync_WhenCalled_ShouldReplaceOnlyThatTasksSessions()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, NullLogger.Instance);
        var sessions = new[] { Entry("Report", 9), Entry("Meeting", 11) };

        var result = await editor.ReplaceSessionsAsync("Report", [Entry("Report", 15)], sessions);

        result.Status.Should().Be(EntryEditStatus.Saved);
        result.Summary.Should().Be("Report");
        saved!.Select(e => e.Start.Hour).Should().BeEquivalentTo(new[] { 15, 11 });
    }

    [Test]
    public async Task UpdateTextAsync_WhenCalled_ShouldRenameTheTasksSessionsAndPersistTheLog()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, NullLogger.Instance);
        var report = Entry("Report", 9, "quarterly");
        var meeting = Entry("Meeting", 11);

        var result = await editor.UpdateTextAsync([report], "Edited", "new element", [report, meeting]);

        result.Status.Should().Be(EntryEditStatus.Saved);
        result.Summary.Should().Be("Edited");
        saved!.Select(e => (e.Task, e.BookingElement)).Should().Equal(("Edited", "new element"), ("Meeting", ""));
    }

    [Test]
    public async Task UpdateTextAsync_WhenTheSaveFails_ShouldReportFailure()
    {
        var editor = new EntryEditor(
            _ => Task.FromException(new IOException("disk full")),
            NullLogger.Instance);
        var sessions = new[] { Entry("Report", 9) };

        var result = await editor.UpdateTextAsync(sessions, "Edited", "", sessions);

        result.Status.Should().Be(EntryEditStatus.Failed);
        result.Error.Should().BeOfType<IOException>();
    }

    [Test]
    public async Task ApplyChangesAsync_WhenChangesArePlanned_ShouldReplaceAndRemoveOnlyThoseSessions()
    {
        IReadOnlyList<TrackerEntry>? saved = null;
        var editor = new EntryEditor(entries => { saved = entries; return Task.CompletedTask; }, NullLogger.Instance);
        var report = Entry("Report", 9);
        var meeting = Entry("Meeting", 11);
        var review = Entry("Review", 13);
        var stretched = report.Clone();
        stretched.Reschedule(report.Start, report.End.AddMinutes(20));

        var result = await editor.ApplyChangesAsync(
            [new SessionChange(report, stretched), new SessionChange(review, null)],
            [report, meeting, review]);

        result.Status.Should().Be(EntryEditStatus.Saved);
        saved!.Select(s => s.Task).Should().Equal("Report", "Meeting");
        saved![0].End.Should().Be(report.End.AddMinutes(20));
        report.End.Should().Be(new DateTimeOffset(2026, 9, 19, 9, 30, 0, TimeSpan.FromHours(2)),
            "the stored session objects are not modified; the saved list holds copies");
    }

    [Test]
    public async Task ApplyChangesAsync_WhenAChangedSessionIsNoLongerInTheLog_ShouldFailWithoutSaving()
    {
        var saves = 0;
        var editor = new EntryEditor(_ => { saves++; return Task.CompletedTask; }, NullLogger.Instance);
        var gone = Entry("Report", 9);

        var result = await editor.ApplyChangesAsync([new SessionChange(gone, null)], [Entry("Report", 9)]);

        result.Status.Should().Be(EntryEditStatus.Failed, "an equal-looking copy is not the planned session");
        saves.Should().Be(0);
    }

    [Test]
    public async Task ApplyChangesAsync_WhenTheSaveFails_ShouldReportFailure()
    {
        var editor = new EntryEditor(_ => Task.FromException(new IOException("disk full")), NullLogger.Instance);
        var report = Entry("Report", 9);

        var result = await editor.ApplyChangesAsync([new SessionChange(report, null)], [report]);

        result.Status.Should().Be(EntryEditStatus.Failed);
        result.Error.Should().BeOfType<IOException>();
    }

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
