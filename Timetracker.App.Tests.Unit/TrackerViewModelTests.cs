using NUnit.Framework;
using AwesomeAssertions;
using FakeItEasy;
using Timetracker.Models;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

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
                BookingElement = "quarterly",
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
    public void History_row_shows_the_latest_session_times_and_the_summed_duration()
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
                Task = "Report",
                Start = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.FromHours(2)),
                Duration = "00:30:00",
                DurationSeconds = 1800,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        var row = vm.Entries.Single();

        row.StartText.Should().Be("2026-09-18 14:00", "Started shows the latest session");
        row.EndText.Should().Be("2026-09-18 14:30", "Ended shows the latest session");
        row.Duration.Should().Be("01:30:00", "Duration sums all sessions");
    }

    [Test]
    public void BookingElement_edit_survives_adding_a_new_session()
    {
        // Regression: the booking element used to revert to empty after a new session.
        var (repo, _) = RepositoryFake.Create(
            new TrackerEntry
            {
                Task = "Report",
                BookingElement = "draft",
                Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            });
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        var row = vm.Entries[0];
        row.BookingElement = "edited via grid"; // binding stages first, as in the real grid
        vm.UpdateEntryText(row, row.Task, row.BookingElement).Should().BeTrue();

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.Entries.Single(r => r.Task == "Report").BookingElement.Should().Be("edited via grid");
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

        var result = vm.UpdateEntryText(row, "   ", row.BookingElement);

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
    public void ApplySort_ignores_the_non_sortable_booking_element_column()
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

        vm.ApplySort(nameof(EntryRow.BookingElement));

        vm.SortColumn.Should().NotBe(nameof(EntryRow.BookingElement));
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
                BookingElement = "quarterly",
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

    [Test]
    public void With_more_than_ten_tasks_the_grid_shows_the_first_page_only()
    {
        var repo = SeedTasks(25);
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.Entries.Should().HaveCount(10);
        vm.TotalPages.Should().Be(3);
        vm.HasMultiplePages.Should().BeTrue();
        vm.CurrentPage.Should().Be(1);
    }

    [Test]
    public void At_most_ten_tasks_need_no_paging()
    {
        var repo = SeedTasks(10);
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.Entries.Should().HaveCount(10);
        vm.TotalPages.Should().Be(1);
        vm.HasMultiplePages.Should().BeFalse();
        vm.NextPageCommand.CanExecute(null).Should().BeFalse();
        vm.PreviousPageCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void Next_and_previous_page_commands_page_through_all_rows()
    {
        var repo = SeedTasks(25);
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.PreviousPageCommand.CanExecute(null).Should().BeFalse("page 1 is the first page");
        vm.NextPageCommand.Execute(null);

        vm.CurrentPage.Should().Be(2);
        vm.Entries.Should().HaveCount(10);
        vm.Entries[0].Task.Should().Be("Task 14", "rows are sorted newest first");
        vm.NextPageCommand.Execute(null);

        vm.CurrentPage.Should().Be(3);
        vm.Entries.Should().HaveCount(5, "the remaining rows of the last page");
        vm.Entries[0].Task.Should().Be("Task 04", "rows are sorted newest first");
        vm.NextPageCommand.CanExecute(null).Should().BeFalse("page 3 is the last page");

        vm.PreviousPageCommand.Execute(null);
        vm.CurrentPage.Should().Be(2);
        vm.Entries.Should().HaveCount(10);
    }

    [Test]
    public void Suggestions_come_from_all_tasks_not_only_the_current_page()
    {
        var repo = SeedTasks(25); // "Task 00" is the oldest row → page 3
        using var vm = new TrackerViewModel(repo, new FakeTimer());

        vm.TaskName = "task 0";

        vm.Suggestions.Select(s => s.Name).Should().Contain("Task 00",
            "suggestions ignore pagination and use all existing items");
    }

    [Test]
    public void Saving_a_new_session_reveals_the_task_row_even_on_another_page()
    {
        var repo = SeedTasks(25);
        using var vm = new TrackerViewModel(repo, new FakeTimer());
        vm.NextPageCommand.Execute(null);
        vm.TaskName = "Brand new task";

        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.CurrentPage.Should().Be(1, "the new row is revealed on its page");
        vm.Entries[0].Task.Should().Be("Brand new task", "it has the newest start time");
    }

    private static ITrackerRepository SeedTasks(int count)
    {
        var entries = Enumerable.Range(0, count).Select(i => new TrackerEntry
        {
            Task = $"Task {i:00}",
            Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)).AddMinutes(i),
            End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)).AddMinutes(i),
            Duration = "01:00:00",
            DurationSeconds = 3600,
        }).ToArray();
        var (repo, _) = RepositoryFake.Create(entries);
        return repo;
    }
}
