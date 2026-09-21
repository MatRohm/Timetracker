using AwesomeAssertions;
using FakeItEasy;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

public sealed class DeleteEntriesTests
{
    [Test]
    public void Delete_removes_only_the_selected_rows_and_persists()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13));
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var row = vm.Entries.Single(r => r.Task == "Meeting");

        var deleted = vm.DeleteEntries([row], _ => true);

        deleted.Should().BeTrue();
        vm.Entries.Select(e => e.Task).Should().BeEquivalentTo(["Report", "Review"]);
        repo.GetAll().Select(e => e.Task).Should().BeEquivalentTo(["Report", "Review"],
            "the repository receives the remaining entries");
    }

    [Test]
    public void Delete_removes_every_session_of_the_task()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Report", 14), Entry("Meeting", 11));
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var row = vm.Entries.Single(r => r.Task == "Report");

        vm.DeleteEntries([row], _ => true);

        repo.GetAll().Should().ContainSingle().Which.Task.Should().Be("Meeting");
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public void Delete_can_remove_multiple_tasks_at_once()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13));
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        var rows = vm.Entries.Where(r => r.Task != "Meeting").ToList();
        var deleted = vm.DeleteEntries(rows, _ => true);

        deleted.Should().BeTrue();
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
        repo.GetAll().Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public void Delete_does_nothing_when_the_user_declines()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9), Entry("Meeting", 11));
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var row = vm.Entries.Single(r => r.Task == "Report");

        var declined = vm.DeleteEntries([row], _ => false);

        declined.Should().BeFalse();
        vm.Entries.Should().HaveCount(2);
        repo.GetAll().Should().HaveCount(2);
        vm.StatusText.Should().NotContain("Deleted");
    }

    [Test]
    public void Delete_with_no_rows_is_a_noop()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9));
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.DeleteEntries([], _ => true).Should().BeFalse();
        vm.DeleteEntries(null!, _ => true).Should().BeFalse();

        vm.Entries.Should().ContainSingle();
        vm.StatusText.Should().NotContain("Deleted");
    }

    [Test]
    public void Failed_delete_rolls_back_the_in_memory_state_and_reports_the_error()
    {
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns("unused.json");
        A.CallTo(() => repo.GetAll()).Returns([Entry("Report", 9), Entry("Meeting", 11)]);
        A.CallTo(() => repo.Save(A<IReadOnlyList<TrackerEntry>>._)).Throws(new IOException("disk full"));
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        string? reported = null;
        vm.ErrorOccurred += m => reported = m;
        var row = vm.Entries.Single(r => r.Task == "Report");

        var deleted = vm.DeleteEntries([row], _ => true);

        deleted.Should().BeFalse();
        vm.Entries.Should().HaveCount(2, "the in-memory state is restored after a failed save");
        vm.Status.Should().Be(TrackerStatus.Error);
        reported.Should().Contain("Could not delete");
    }

    [Test]
    public void Delete_writes_the_remaining_entries_to_the_real_file()
    {
        // End-to-end with the real repository: file contents after delete.
        var dir = Path.Combine(Path.GetTempPath(), "opencode", "tt-delete-tests");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"tt-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            [
              { "task": "Report", "bookingElement": "", "start": "2026-09-19T09:00:00+02:00", "end": "2026-09-19T09:30:00+02:00", "duration": "00:30:00", "durationSeconds": 1800 },
              { "task": "Meeting", "bookingElement": "", "start": "2026-09-19T11:00:00+02:00", "end": "2026-09-19T11:30:00+02:00", "duration": "00:30:00", "durationSeconds": 1800 }
            ]
            """);
        using var vm = new TrackerViewModel(new JsonTrackerRepository(path), new FakeTimer());
        var row = vm.Entries.Single(r => r.Task == "Report");

        vm.DeleteEntries([row], _ => true);

        var onDisk = new JsonTrackerRepository(path).GetAll();
        onDisk.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    private static TrackerEntry Entry(string task, int hour) => new()
    {
        Task = task,
        Start = new DateTimeOffset(2026, 9, 19, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 19, hour, 30, 0, TimeSpan.FromHours(2)),
        Duration = "00:30:00",
        DurationSeconds = 1800,
    };
}
