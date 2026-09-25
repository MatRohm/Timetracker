using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

/// <summary>
/// Behavior of the per-item session editor dialog: it shows every session of one
/// tracking item, lets each session's start/end be edited (duration is derived)
/// and lets individual sessions be deleted. Saving an edited item persists exactly
/// its sessions, leaves other tasks untouched, and keeps the history rows and week
/// view in sync.
/// </summary>
[TestFixture]
public sealed class EditEntriesViewModelTests
{
    [Test]
    public void Sessions_WhenTheItemHasSessions_ShouldListThemChronologically()
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
    public void SummaryText_WhenTotalsAreEdited_ShouldReflectThem()
    {
        var editor = new EditEntriesViewModel(Item("Report",
            Session("Report", day: 18, hour: 9, minutes: 30),
            Session("Report", day: 18, hour: 14, minutes: 60)));

        // 30 + 60 minutes = 1:30:00 across two sessions.
        editor.SummaryText.Should().Contain("2");
        editor.SummaryText.Should().Contain("01:30:00");
    }

    [Test]
    public void CanSave_WhenTheItemHasNoSessions_ShouldBeFalse()
    {
        var editor = new EditEntriesViewModel(new EntryRow([]));

        editor.Sessions.Should().BeEmpty();
        editor.CanSave.Should().BeFalse();
    }

    [Test]
    public void RemoveSession_WhenInvoked_ShouldRemoveOnlyThatSession()
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
    public void Save_WhenEditedTimesAreSaved_ShouldPersistThemAndUpdateTheHistoryRow()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 18, 9, 30), Entry("Meeting", 18, 11, 60));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var item = vm.Entries.Single(r => r.Task == "Report");

        var editor = new EditEntriesViewModel(item);
        var row = editor.Sessions.Single();
        row.SetStart(row.Start.AddMinutes(-30));
        row.SetEnd(row.End.AddMinutes(30));
        editor.Save();

        vm.ReplaceSessions(item.Task, editor.RemainingEntries()).Should().BeTrue();

        var stored = repo.GetAll().Single(e => e.Task == "Report");
        stored.DurationSeconds.Should().Be(5400, "30 + 30 minutes are added to the original hour");
        stored.Duration.Should().Be("01:30:00");
        vm.Entries.Single(r => r.Task == "Report").Duration.Should().Be("01:30:00");
    }

    [Test]
    public void Save_WhenAnItemIsSaved_ShouldLeaveOtherItemsUntouched()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 18, 9, 30), Entry("Meeting", 18, 11, 60));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var item = vm.Entries.Single(r => r.Task == "Report");
        var editor = new EditEntriesViewModel(item);
        editor.Sessions.Single().SetEnd(editor.Sessions.Single().End.AddMinutes(15));
        editor.Save();

        vm.ReplaceSessions(item.Task, editor.RemainingEntries());

        repo.GetAll().Single(e => e.Task == "Meeting").DurationSeconds.Should().Be(3600);
    }

    [Test]
    public void RemoveSession_WhenTheLastSessionIsDeleted_ShouldRemoveTheHistoryRow()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 18, 9, 30), Entry("Meeting", 18, 11, 60));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var item = vm.Entries.Single(r => r.Task == "Report");
        var editor = new EditEntriesViewModel(item);

        editor.RemoveSession(editor.Sessions.Single());
        vm.ReplaceSessions(item.Task, editor.RemainingEntries());

        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
        repo.GetAll().Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public void RemoveSession_WhenOneOfSeveralSessionsIsDeleted_ShouldKeepTheItemWithTheRemainingTime()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 18, 9, 30), Entry("Report", 18, 14, 60));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var item = vm.Entries.Single(r => r.Task == "Report");
        item.Sessions.Should().HaveCount(2);
        var editor = new EditEntriesViewModel(item);

        // Remove the 60-minute session, keep the 30-minute one.
        editor.RemoveSession(editor.Sessions.Single(s => s.DurationSeconds == 3600));
        vm.ReplaceSessions(item.Task, editor.RemainingEntries());

        var row = vm.Entries.Single();
        row.Sessions.Should().ContainSingle();
        row.Duration.Should().Be("00:30:00");
        repo.GetAll().Should().ContainSingle().Which.DurationSeconds.Should().Be(1800);
    }

    [Test]
    public void Save_WhenSessionsAreSaved_ShouldUpdateTheWeekViewTotals()
    {
        // Use today so the session lands in the week the view starts on.
        var today = DateTimeOffset.Now.Date.AddHours(9);
        var entry = new TrackerEntry
        {
            Task = "Report",
            Start = new DateTimeOffset(today, DateTimeOffset.Now.Offset),
            End = new DateTimeOffset(today.AddMinutes(30), DateTimeOffset.Now.Offset),
            Duration = "00:30:00",
            DurationSeconds = 1800,
        };
        var (repo, _) = RepositoryFake.Create(entry);
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var item = vm.Entries.Single();
        var editor = new EditEntriesViewModel(item);
        var row = editor.Sessions.Single();
        row.SetEnd(row.End.AddHours(1));
        editor.Save();

        vm.ReplaceSessions(item.Task, editor.RemainingEntries());

        // The edited duration (1:30) shows in the today column of the week view.
        vm.Week.Days.Single(d => d.IsToday).EntriesText.Should().Contain("1:30");
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

    private static TrackerEntry Entry(string task, int day, int hour, int minutes)
    {
        var start = new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.FromHours(2));
        return new TrackerEntry
        {
            Task = task,
            Start = start,
            End = start.AddMinutes(minutes),
            Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
            DurationSeconds = minutes * 60.0,
        };
    }
}
