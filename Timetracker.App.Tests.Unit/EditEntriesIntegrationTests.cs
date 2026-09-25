using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

/// <summary>
/// Integration between the per-item editor and the tracker: saving an edited item
/// persists exactly its sessions, leaves other tasks untouched, and keeps the
/// history rows and week view in sync.
/// </summary>
public sealed class EditEntriesIntegrationTests
{
    [Test]
    public void EditEntriesViewModel_WhenEditedTimesAreSaved_ShouldPersistThemAndUpdateHistoryRow()
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
    public void EditEntriesViewModel_WhenAnItemIsSaved_ShouldLeaveOtherItemsUntouched()
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
    public void EditEntriesViewModel_WhenLastSessionIsDeleted_ShouldRemoveHistoryRow()
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
    public void EditEntriesViewModel_WhenOneSessionIsDeleted_ShouldKeepItemWithRemainingTime()
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
    public void EditEntriesViewModel_WhenSessionsAreSaved_ShouldUpdateWeekViewTotals()
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
