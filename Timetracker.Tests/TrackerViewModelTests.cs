using NUnit.Framework;
using AwesomeAssertions;
using FakeItEasy;
using Timetracker.Models;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Tests;

public sealed class TrackerViewModelTests
{
    [Test]
    public void Start_with_task_name_starts_the_timer()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.IsRunning.Should().BeTrue();
    }

    [Test]
    public void Start_without_task_name_is_rejected_and_raises_invalid_task_name()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var invalidNameRaised = false;
        vm.InvalidTaskName += () => invalidNameRaised = true;

        vm.TaskName = "   ";
        vm.StartCommand.Execute(null);

        vm.IsRunning.Should().BeFalse();
        invalidNameRaised.Should().BeTrue();
    }

    [Test]
    public void Start_is_blocked_while_running()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.StartFromRow(vm.Entries[0]);

        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report", "a running session must not be switched");
    }

    [Test]
    public void Stop_appends_a_session_and_resets_the_running_state()
    {
        var (repo, path) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.StopCommand.Execute(null);

        vm.IsRunning.Should().BeFalse();
        repo.GetAll().Should().HaveCount(1);
        var entry = repo.GetAll().Single();
        entry.Task.Should().Be("Report");
        entry.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public void New_session_on_existing_task_is_grouped_and_accumulates_duration()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Description = "quarterly",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.StartFromRow(vm.Entries.Single(r => r.Task == "Report"));
        Thread.Sleep(50);
        vm.StopCommand.Execute(null);

        vm.Entries.Should().HaveCount(1, "same task name groups into one row");
        vm.Entries[0].Sessions.Should().HaveCount(2);
        vm.Entries[0].DurationSeconds.Should().BeGreaterThan(3600, "time is added to the task");
    }

    [Test]
    public void Description_edit_survives_adding_a_new_session()
    {
        // Regression: the description used to revert to empty after a new session.
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Description = "draft",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var row = vm.Entries[0];
        row.Description = "edited via grid"; // binding stages first, as in the real grid
        vm.UpdateEntryText(row, row.Task, row.Description).Should().BeTrue();

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.Entries.Single(r => r.Task == "Report").Description.Should().Be("edited via grid");
    }

    [Test]
    public void UpdateEntryText_rejects_empty_task_name()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var row = vm.Entries[0];

        var result = vm.UpdateEntryText(row, "   ", row.Description);

        result.Should().BeFalse();
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Report");
    }

    [Test]
    public void Suggestions_are_substring_matches_with_totals_and_exclude_exact_match()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            },
            new TrackerEntry
            {
                Task = "Meeting",
                Start = new DateTimeOffset(2026, 9, 18, 11, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 11, 30, 0, TimeSpan.FromHours(2)),
                Duration = "00:30:00",
                DurationSeconds = 1800,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.TaskName = "rep";

        vm.Suggestions.Should().ContainSingle().Which.Name.Should().Be("Report");
        vm.Suggestions[0].TotalText.Should().Be("01:00:00");

        vm.TaskName = "report";
        vm.Suggestions.Should().BeEmpty("an exact match is not suggested");
    }

    [Test]
    public void AcceptSuggestion_fills_the_task_name()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Meeting",
                Start = new DateTimeOffset(2026, 9, 18, 11, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 11, 30, 0, TimeSpan.FromHours(2)),
                Duration = "00:30:00",
                DurationSeconds = 1800,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        vm.TaskName = "mee";

        vm.AcceptSuggestion(vm.Suggestions.Single());

        vm.TaskName.Should().Be("Meeting");
    }

    [Test]
    public void ApplySort_ignores_the_non_sortable_description_column()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.ApplySort(nameof(EntryRow.Description));

        vm.SortColumn.Should().NotBe(nameof(EntryRow.Description));
        vm.SortColumn.Should().Be(nameof(EntryRow.StartText), "the previous sort is kept");
    }

    [Test]
    public void ApplySort_toggles_direction_on_repeated_clicks()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.ApplySort(nameof(EntryRow.Task));
        vm.SortAscending.Should().BeTrue();

        vm.ApplySort(nameof(EntryRow.Task));
        vm.SortAscending.Should().BeFalse();
    }

    [Test]
    public void StartFromRow_fills_the_task_field_and_starts()
    {
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                Description = "quarterly",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.StartFromRow(vm.Entries[0]);

        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report");
    }

    [Test]
    public void Save_failure_raises_error_and_keeps_state_consistent()
    {
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns(Path.Combine(Path.GetTempPath(), "does-not-matter.json"));
        A.CallTo(() => repo.Add(A<TrackerEntry>._)).Throws(new IOException("disk full"));
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        string? reported = null;
        vm.ErrorOccurred += m => reported = m;
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.StopCommand.Execute(null);

        vm.IsRunning.Should().BeFalse("the session ends even if saving fails");
        reported.Should().Contain("disk full");
        vm.Status.Should().Be(TrackerStatus.Error);
    }
}
