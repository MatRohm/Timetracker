using Timetracker.Interfaces;
using NUnit.Framework;
using AwesomeAssertions;
using FakeItEasy;
using Timetracker.Models;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

[TestFixture]
public sealed class TrackerViewModelTests
{
    [Test]
    public void Start_WhenTaskNameIsSet_ShouldStartTheTimer()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.IsRunning.Should().BeTrue();
    }

    [Test]
    public void Start_WhenTheTaskNameIsEmpty_ShouldRejectAndRaiseInvalidTaskName()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var invalidNameRaised = false;
        vm.InvalidTaskName += () => invalidNameRaised = true;

        vm.TaskName = "   ";
        vm.StartCommand.Execute(null);

        vm.IsRunning.Should().BeFalse();
        invalidNameRaised.Should().BeTrue();
    }

    [Test]
    public void StartFromRow_WhenAlreadyRunning_ShouldNotSwitchTheRunningSession()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.StartFromRow(vm.Entries[0]);

        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report", "a running session must not be switched");
    }

    [Test]
    public void Stop_WhenStopped_ShouldAppendSessionAndResetRunningState()
    {
        var (repo, path) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
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
    public void StartFromRow_WhenTheTaskAlreadyExists_ShouldGroupAndAccumulateDuration()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.StartFromRow(vm.Entries.Single(r => r.Task == "Report"));
        Thread.Sleep(50);
        vm.StopCommand.Execute(null);

        vm.Entries.Should().HaveCount(1, "same task name groups into one row");
        vm.Entries[0].Sessions.Should().HaveCount(2);
        vm.Entries[0].DurationSeconds.Should().BeGreaterThan(3600, "time is added to the task");
    }

    [Test]
    public void Entries_WhenATaskHasMultipleSessions_ShouldShowLatestTimesAndSummedDuration()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        var row = vm.Entries.Single();

        row.StartText.Should().Be("2026-09-18 14:00", "Started shows the latest session");
        row.EndText.Should().Be("2026-09-18 14:30", "Ended shows the latest session");
        row.Duration.Should().Be("01:30:00", "Duration sums all sessions");
    }

    [Test]
    public void UpdateEntryText_WhenTheBookingElementIsEdited_ShouldSurviveAddingANewSession()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var row = vm.Entries[0];
        row.BookingElement = "edited via grid"; // binding stages first, as in the real grid
        vm.UpdateEntryText(row, row.Task, row.BookingElement).Should().BeTrue();

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.Entries.Single(r => r.Task == "Report").BookingElement.Should().Be("edited via grid");
    }

    [Test]
    public void UpdateEntryText_WhenTheTaskNameIsEmpty_ShouldReject()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var row = vm.Entries[0];

        var result = vm.UpdateEntryText(row, "   ", row.BookingElement);

        result.Should().BeFalse();
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Report");
    }

    [Test]
    public void TaskName_WhenTheQueryIsASubstring_ShouldSuggestMatchesWithTotalsAndExcludeExactMatch()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.TaskName = "rep";

        vm.Suggestions.Should().ContainSingle().Which.Name.Should().Be("Report");
        vm.Suggestions[0].TotalText.Should().Be("01:00:00");

        vm.TaskName = "report";
        vm.Suggestions.Should().BeEmpty("an exact match is not suggested");
    }

    [Test]
    public void AcceptSuggestion_WhenAccepted_ShouldFillTheTaskName()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        vm.TaskName = "mee";

        vm.AcceptSuggestion(vm.Suggestions.Single());

        vm.TaskName.Should().Be("Meeting");
    }

    [Test]
    public void ApplySort_WhenTheBookingElementColumnIsChosen_ShouldIgnoreIt()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.ApplySort(nameof(EntryRow.BookingElement));

        vm.SortColumn.Should().NotBe(nameof(EntryRow.BookingElement));
        vm.SortColumn.Should().Be(nameof(EntryRow.StartText), "the previous sort is kept");
    }

    [Test]
    public void ApplySort_WhenTheSameColumnIsChosenTwice_ShouldToggleDirection()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.ApplySort(nameof(EntryRow.Task));
        vm.SortAscending.Should().BeTrue();

        vm.ApplySort(nameof(EntryRow.Task));
        vm.SortAscending.Should().BeFalse();
    }

    [Test]
    public void StartFromRow_WhenInvoked_ShouldFillTheTaskFieldAndStart()
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
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.StartFromRow(vm.Entries[0]);

        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report");
    }

    [Test]
    public void Stop_WhenSavingFails_ShouldRaiseAnErrorAndKeepStateConsistent()
    {
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns(Path.Combine(Path.GetTempPath(), "does-not-matter.json"));
        A.CallTo(() => repo.Add(A<TrackerEntry>._)).Throws(new IOException("disk full"));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
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
    public void Entries_WhenThereAreMoreThanTenTasks_ShouldShowTheFirstPageOnly()
    {
        var repo = SeedTasks(25);
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.Entries.Should().HaveCount(10);
        vm.TotalPages.Should().Be(3);
        vm.HasMultiplePages.Should().BeTrue();
        vm.CurrentPage.Should().Be(1);
    }

    [Test]
    public void TotalPages_WhenThereAreTenTasksOrFewer_ShouldBeOne()
    {
        var repo = SeedTasks(10);
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.Entries.Should().HaveCount(10);
        vm.TotalPages.Should().Be(1);
        vm.HasMultiplePages.Should().BeFalse();
        vm.NextPageCommand.CanExecute(null).Should().BeFalse();
        vm.PreviousPageCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void NextPageCommand_WhenPagingBackAndForth_ShouldCoverAllRows()
    {
        var repo = SeedTasks(25);
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

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
    public void Suggestions_WhenTheTaskIsOnAnotherPage_ShouldUseAllTasks()
    {
        var repo = SeedTasks(25); // "Task 00" is the oldest row → page 3
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.TaskName = "task 0";

        vm.Suggestions.Select(s => s.Name).Should().Contain("Task 00",
            "suggestions ignore pagination and use all existing items");
    }

    [Test]
    public void StartCommand_WhenSavingANewSessionOnAnotherPage_ShouldRevealTheTaskRow()
    {
        var repo = SeedTasks(25);
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        vm.NextPageCommand.Execute(null);
        vm.TaskName = "Brand new task";

        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.CurrentPage.Should().Be(1, "the new row is revealed on its page");
        vm.Entries[0].Task.Should().Be("Brand new task", "it has the newest start time");
    }

    [Test]
    public void DeleteEntries_WhenSelectingSomeRows_ShouldRemoveOnlyThoseAndPersist()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var row = vm.Entries.Single(r => r.Task == "Meeting");

        var deleted = vm.DeleteEntries([row], _ => true);

        deleted.Should().BeTrue();
        vm.Entries.Select(e => e.Task).Should().BeEquivalentTo(["Report", "Review"]);
        repo.GetAll().Select(e => e.Task).Should().BeEquivalentTo(["Report", "Review"],
            "the repository receives the remaining entries");
    }

    [Test]
    public void DeleteEntries_WhenDeletingATask_ShouldRemoveEverySession()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Report", 14), Entry("Meeting", 11));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var row = vm.Entries.Single(r => r.Task == "Report");

        vm.DeleteEntries([row], _ => true);

        repo.GetAll().Should().ContainSingle().Which.Task.Should().Be("Meeting");
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public void DeleteEntries_WhenSelectingMultipleTasks_ShouldRemoveThemAll()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        var rows = vm.Entries.Where(r => r.Task != "Meeting").ToList();
        var deleted = vm.DeleteEntries(rows, _ => true);

        deleted.Should().BeTrue();
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
        repo.GetAll().Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public void DeleteEntries_WhenUserDeclines_ShouldDoNothing()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9), Entry("Meeting", 11));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var row = vm.Entries.Single(r => r.Task == "Report");

        var declined = vm.DeleteEntries([row], _ => false);

        declined.Should().BeFalse();
        vm.Entries.Should().HaveCount(2);
        repo.GetAll().Should().HaveCount(2);
        vm.StatusText.Should().NotContain("Deleted");
    }

    [Test]
    public void DeleteEntries_WhenThereAreNoRows_ShouldDoNothing()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        vm.DeleteEntries([], _ => true).Should().BeFalse();
        vm.DeleteEntries(null!, _ => true).Should().BeFalse();

        vm.Entries.Should().ContainSingle();
        vm.StatusText.Should().NotContain("Deleted");
    }

    [Test]
    public void DeleteEntries_WhenTheSaveFails_ShouldRollBackStateAndReportError()
    {
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns("unused.json");
        A.CallTo(() => repo.GetAll()).Returns([Entry("Report", 9), Entry("Meeting", 11)]);
        A.CallTo(() => repo.Save(A<IReadOnlyList<TrackerEntry>>._)).Throws(new IOException("disk full"));
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
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
    public void DeleteEntries_WhenItSucceeds_ShouldWriteRemainingEntriesToFile()
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
        using var vm = new TrackerViewModel(new JsonTrackerRepository(path), new FakeTimer(), new FakeIdleTimeProvider());
        var row = vm.Entries.Single(r => r.Task == "Report");

        vm.DeleteEntries([row], _ => true);

        var onDisk = new JsonTrackerRepository(path).GetAll();
        onDisk.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    /// <summary>
    /// Idle auto-stop: when there has been no keyboard/mouse input for the threshold,
    /// the running session is stopped and recorded up to the moment the idle stretch
    /// began, so idle time is not billed to the task.
    /// </summary>
    [Test]
    public void OnTimerTick_WhenIdleThresholdIsReached_ShouldStopRunningSession()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.IsRunning.Should().BeTrue();

        idle.CurrentIdleTime = TrackerViewModel.IdleStopThreshold;
        timer.RaiseTick();

        vm.IsRunning.Should().BeFalse("30 minutes without input stops the timer");
        repo.GetAll().Should().ContainSingle("the session was saved");
    }

    [Test]
    public void OnTimerTick_WhenIdleIsBelowThreshold_ShouldKeepSessionRunning()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        idle.CurrentIdleTime = TrackerViewModel.IdleStopThreshold - TimeSpan.FromSeconds(1);
        timer.RaiseTick();

        vm.IsRunning.Should().BeTrue("the threshold is 30 minutes, not less");
        repo.GetAll().Should().BeEmpty();
    }

    [Test]
    public void StopForIdle_WhenAnIdleSpanOccurs_ShouldNotCountItAsWorkTime()
    {
        // A 15-minute session, then 45 minutes away: only the 15 worked minutes
        // are billed, and the entry ends when the user walked away.
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        var now = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.FromHours(2));
        using var vm = new TrackerViewModel(repo, timer, idle, () => now);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        // 15 minutes of work, then the machine sits idle for 45 minutes.
        var worked = TimeSpan.FromMinutes(15);
        var idleFor = TimeSpan.FromMinutes(45);
        now += worked + idleFor;
        idle.CurrentIdleTime = idleFor;
        timer.RaiseTick();

        var entry = repo.GetAll().Single();
        entry.DurationSeconds.Should().Be(worked.TotalSeconds, "only worked time is billed");
        entry.Duration.Should().Be("00:15:00");
        entry.End.Should().Be(now - idleFor, "the entry ends when the idle stretch began");
    }

    [Test]
    public void StopForIdle_WhenItStops_ShouldExplainItInTheStatusLine()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        idle.CurrentIdleTime = TimeSpan.FromMinutes(40);
        timer.RaiseTick();

        vm.StatusText.Should().Contain("40 min", "the idle duration is reported");
        vm.Status.Should().Be(TrackerStatus.Info);
    }

    [Test]
    public void OnTimerTick_WhenTheSessionIsAlreadyStopped_ShouldNotStopItAgain()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        idle.CurrentIdleTime = TimeSpan.FromMinutes(45);
        timer.RaiseTick();
        timer.RaiseTick();
        timer.RaiseTick();

        repo.GetAll().Should().ContainSingle("only one entry is saved");
    }

    [Test]
    public void OnTimerTick_WhenTheSessionIsNotRunning_ShouldNotStopItForIdle()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);

        idle.CurrentIdleTime = TimeSpan.FromHours(2);
        timer.RaiseTick();

        vm.IsRunning.Should().BeFalse();
        repo.GetAll().Should().BeEmpty("nothing was running, so nothing is saved");
    }

    [Test]
    public void Start_WhenStartingAgainAfterAnIdleStop_ShouldWorkNormally()
    {
        var (repo, _) = RepositoryFake.Create();
        var timer = new FakeTimer();
        var idle = new FakeIdleTimeProvider();
        using var vm = new TrackerViewModel(repo, timer, idle);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        idle.CurrentIdleTime = TimeSpan.FromMinutes(45);
        timer.RaiseTick();

        // Back at the machine: idle resets and a new session can start.
        idle.CurrentIdleTime = TimeSpan.Zero;
        vm.StartCommand.Execute(null);
        vm.IsRunning.Should().BeTrue();
        vm.StopCommand.Execute(null);

        repo.GetAll().Should().HaveCount(2, "the idle-stopped session and the new one");
    }

    /// <summary>
    /// Covers the tracker view model's preview-field behavior. The Azure DevOps
    /// add-in fills these fields; the tracker must consume them for exactly one
    /// session, so the test lives with the tracker rather than the add-in.
    /// </summary>
    [Test]
    public void BuildEntry_WhenAPreviewBookingElementIsSet_ShouldUseItForTheNextSessionThenClearIt()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        vm.PreviewBookingElement = "Quarterly figures";

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        repo.GetAll().Single().BookingElement.Should().Be("Quarterly figures");
        vm.PreviewBookingElement.Should().BeEmpty("the preview is consumed by one session");
    }

    private static TrackerEntry Entry(string task, int hour) => new()
    {
        Task = task,
        Start = new DateTimeOffset(2026, 9, 19, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 19, hour, 30, 0, TimeSpan.FromHours(2)),
        Duration = "00:30:00",
        DurationSeconds = 1800,
    };

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
