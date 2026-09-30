using Timetracker.App.Interfaces;
using NUnit.Framework;
using AwesomeAssertions;
using FakeItEasy;
using Timetracker.App.Models;
using Timetracker.App.Services;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Tests.Unit.ViewModels;

[TestFixture]
public sealed class TrackerViewModelTests
{
    [Test]
    public void Start_WhenTaskNameIsSet_ShouldStartTheTimer()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.IsRunning.Should().BeTrue();
    }

    [Test]
    public void Start_WhenTheTaskNameIsEmpty_ShouldRejectAndRaiseInvalidTaskName()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
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
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.StartFromRow(vm.Entries[0]);

        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report", "a running session must not be switched");
    }

    [Test]
    public async Task Stop_WhenStopped_ShouldAppendSessionAndResetRunningState()
    {
        var (repo, path) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.StopCommand.Execute(null);

        vm.IsRunning.Should().BeFalse();
        (await repo.GetAllAsync()).Should().HaveCount(1);
        var entry = (await repo.GetAllAsync()).Single();
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
        using var vm = TrackerViewModelFactory.Create(repo);

        vm.StartFromRow(vm.Entries.Single(r => r.Task == "Report"));
        Thread.Sleep(50);
        vm.StopCommand.Execute(null);

        vm.Entries.Should().HaveCount(1, "same task name groups into one row");
        vm.Entries[0].Sessions.Should().HaveCount(2);
        vm.Entries[0].DurationSeconds.Should().BeGreaterThan(3600, "time is added to the task");
    }

    [Test]
    public async Task UpdateEntryTextAsync_WhenTheBookingElementIsEdited_ShouldSurviveAddingANewSession()
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
        using var vm = TrackerViewModelFactory.Create(repo);
        var row = vm.Entries[0];
        row.BookingElement = "edited via grid"; // binding stages first, as in the real grid
        (await vm.UpdateEntryTextAsync(row, row.Task, row.BookingElement)).Should().BeTrue();

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.Entries.Single(r => r.Task == "Report").BookingElement.Should().Be("edited via grid");
    }

    [Test]
    public async Task UpdateEntryTextAsync_WhenTheTaskNameIsEmpty_ShouldReject()
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
        using var vm = TrackerViewModelFactory.Create(repo);
        var row = vm.Entries[0];

        var result = await vm.UpdateEntryTextAsync(row, "   ", row.BookingElement);

        result.Should().BeFalse();
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Report");
    }

    [Test]
    public async Task UpdateEntryTextAsync_WhenNothingChanged_ShouldRestoreTheCommittedTextWithoutSaving()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9));
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        var row = vm.Entries.Single();
        row.BookingElement = "staged but uncommitted";

        var result = await vm.UpdateEntryTextAsync(row, "Report", "");

        result.Should().BeTrue();
        row.CommittedBookingElement.Should().BeEmpty("the committed snapshot is unchanged");
        A.CallTo(() => repo.SaveAsync(A<IReadOnlyList<TrackerEntry>>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task UpdateEntryTextAsync_WhenChanged_ShouldPersistAndCommitTheRow()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", "quarterly", 9));
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        var row = vm.Entries.Single();

        var result = await vm.UpdateEntryTextAsync(row, " Edited ", "new element");

        result.Should().BeTrue();
        row.CommittedTask.Should().Be("Edited", "the task name is trimmed");
        row.CommittedBookingElement.Should().Be("new element");
        RepositoryFake.Persisted(repo).Single().Task.Should().Be("Edited");
    }

    [Test]
    public void BuildDeleteSummary_WhenOneRow_ShouldNameTheTaskAndItsSessionCount()
    {
        var row = new EntryRow([Entry("Report", 9), Entry("Report", 14)]);

        TrackerViewModel.BuildDeleteSummary([row]).Should().Be("\"Report\" (all 2 sessions)");
    }

    [Test]
    public void BuildDeleteSummary_WhenSeveralRows_ShouldCountTasksAndSessions()
    {
        var rows = new[] { new EntryRow([Entry("Report", 9)]), new EntryRow([Entry("Meeting", 11), Entry("Meeting", 14)]) };

        TrackerViewModel.BuildDeleteSummary(rows).Should().Be("2 tasks (3 sessions)");
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
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.TaskName = "mee";

        vm.AcceptSuggestion(vm.Suggestions.Single());

        vm.TaskName.Should().Be("Meeting");
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
        using var vm = TrackerViewModelFactory.Create(repo);

        vm.StartFromRow(vm.Entries[0]);

        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report");
    }

    [Test]
    public void Stop_WhenSavingFails_ShouldRaiseAnErrorAndKeepStateConsistent()
    {
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns(Path.Combine(Path.GetTempPath(), "does-not-matter.json"));
        A.CallTo(() => repo.AddAsync(A<TrackerEntry>._)).ThrowsAsync(new IOException("disk full"));
        using var vm = TrackerViewModelFactory.Create(repo);
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
    public void Suggestions_WhenTheTaskIsOnAnotherPage_ShouldUseAllTasks()
    {
        var repo = SeedTasks(25); // "Task 00" is the oldest row → page 3
        using var vm = TrackerViewModelFactory.Create(repo);

        vm.TaskName = "task 0";

        vm.Suggestions.Select(s => s.Name).Should().Contain("Task 00",
            "suggestions ignore pagination and use all existing items");
    }

    [Test]
    public void StartCommand_WhenSavingANewSessionOnAnotherPage_ShouldRevealTheTaskRow()
    {
        var repo = SeedTasks(25);
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.NextPageCommand.Execute(null);
        vm.TaskName = "Brand new task";

        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.CurrentPage.Should().Be(1, "the new row is revealed on its page");
        vm.Entries[0].Task.Should().Be("Brand new task", "it has the newest start time");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenSelectingSomeRows_ShouldRemoveOnlyThoseAndPersist()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13));
        using var vm = TrackerViewModelFactory.Create(repo);
        var row = vm.Entries.Single(r => r.Task == "Meeting");

        var deleted = await vm.DeleteEntriesAsync([row], _ => true);

        deleted.Should().BeTrue();
        vm.Entries.Select(e => e.Task).Should().BeEquivalentTo(["Report", "Review"]);
        (await repo.GetAllAsync()).Select(e => e.Task).Should().BeEquivalentTo(["Report", "Review"],
            "the repository receives the remaining entries");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenDeletingATask_ShouldRemoveEverySession()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Report", 14), Entry("Meeting", 11));
        using var vm = TrackerViewModelFactory.Create(repo);
        var row = vm.Entries.Single(r => r.Task == "Report");

        await vm.DeleteEntriesAsync([row], _ => true);

        (await repo.GetAllAsync()).Should().ContainSingle().Which.Task.Should().Be("Meeting");
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenSelectingMultipleTasks_ShouldRemoveThemAll()
    {
        var (repo, _) = RepositoryFake.Create(
            Entry("Report", 9), Entry("Meeting", 11), Entry("Review", 13));
        using var vm = TrackerViewModelFactory.Create(repo);

        var rows = vm.Entries.Where(r => r.Task != "Meeting").ToList();
        var deleted = await vm.DeleteEntriesAsync(rows, _ => true);

        deleted.Should().BeTrue();
        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
        (await repo.GetAllAsync()).Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenUserDeclines_ShouldDoNothing()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9), Entry("Meeting", 11));
        using var vm = TrackerViewModelFactory.Create(repo);
        var row = vm.Entries.Single(r => r.Task == "Report");

        var declined = await vm.DeleteEntriesAsync([row], _ => false);

        declined.Should().BeFalse();
        vm.Entries.Should().HaveCount(2);
        (await repo.GetAllAsync()).Should().HaveCount(2);
        vm.StatusText.Should().NotContain("Deleted");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenThereAreNoRows_ShouldDoNothing()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9));
        using var vm = TrackerViewModelFactory.Create(repo);

        (await vm.DeleteEntriesAsync([], _ => true)).Should().BeFalse();
        (await vm.DeleteEntriesAsync(null!, _ => true)).Should().BeFalse();

        vm.Entries.Should().ContainSingle();
        vm.StatusText.Should().NotContain("Deleted");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenTheSaveFails_ShouldRollBackStateAndReportError()
    {
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns("unused.json");
        A.CallTo(() => repo.GetAllAsync()).Returns(
            Task.FromResult<IReadOnlyList<TrackerEntry>>([Entry("Report", 9), Entry("Meeting", 11)]));
        A.CallTo(() => repo.SaveAsync(A<IReadOnlyList<TrackerEntry>>._)).ThrowsAsync(new IOException("disk full"));
        using var vm = TrackerViewModelFactory.Create(repo);
        string? reported = null;
        vm.ErrorOccurred += m => reported = m;
        var row = vm.Entries.Single(r => r.Task == "Report");

        var deleted = await vm.DeleteEntriesAsync([row], _ => true);

        deleted.Should().BeFalse();
        vm.Entries.Should().HaveCount(2, "the in-memory state is restored after a failed save");
        vm.Status.Should().Be(TrackerStatus.Error);
        reported.Should().Contain("Could not delete");
    }

    [Test]
    public async Task DeleteEntriesAsync_WhenItSucceeds_ShouldWriteRemainingEntriesToFile()
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
        using var vm = TrackerViewModelFactory.Create(new JsonTrackerRepository(path));
        await vm.InitialLoad;
        var row = vm.Entries.Single(r => r.Task == "Report");

        await vm.DeleteEntriesAsync([row], _ => true);

        var onDisk = await new JsonTrackerRepository(path).GetAllAsync();
        onDisk.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    /// <summary>
    /// The session host's start path: an add-in starts a session by name; a running
    /// session and an empty task name are both refused.
    /// </summary>
    [Test]
    public async Task StartSessionAsync_WhenTaskNameIsGiven_ShouldStartTheSession()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);

        var started = await vm.StartSessionAsync("Report", "AZE-123");

        started.Should().BeTrue();
        vm.IsRunning.Should().BeTrue();
        vm.TaskName.Should().Be("Report");
    }

    [Test]
    public async Task StartSessionAsync_WhenSessionIsAlreadyRunning_ShouldReturnFalse()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        var started = await vm.StartSessionAsync("Other");

        started.Should().BeFalse("a session is already running");
    }

    [Test]
    public async Task StartSessionAsync_WhenTaskNameIsEmpty_ShouldReturnFalse()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);

        var started = await vm.StartSessionAsync("   ");

        started.Should().BeFalse();
        vm.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task StopSessionAsync_WhenRunning_ShouldSaveTheSessionAndReturnTrue()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        var stopped = await vm.StopSessionAsync();

        stopped.Should().BeTrue();
        vm.IsRunning.Should().BeFalse();
        (await repo.GetAllAsync()).Should().ContainSingle("the session was saved");
    }

    [Test]
    public async Task StopSessionAsync_WhenNotRunning_ShouldReturnFalse()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);

        var stopped = await vm.StopSessionAsync();

        stopped.Should().BeFalse();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task StopSessionAsync_WhenEndedAtIsBackDated_ShouldNotCountTheIdleStretch()
    {
        // A 15-minute session stopped after 45 minutes away: only the 15 worked
        // minutes are billed, and the entry ends when the user walked away.
        var (repo, _) = RepositoryFake.Create();
        var now = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.FromHours(2));
        using var vm = TrackerViewModelFactory.Create(repo, now: () => now);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        var worked = TimeSpan.FromMinutes(15);
        var idleFor = TimeSpan.FromMinutes(45);
        now += worked + idleFor;

        await vm.StopSessionAsync(now - idleFor);

        var entry = (await repo.GetAllAsync()).Single();
        entry.DurationSeconds.Should().Be(worked.TotalSeconds, "only worked time is billed");
        entry.Duration.Should().Be("00:15:00");
        entry.End.Should().Be(now - idleFor, "the entry ends when the idle stretch began");
    }

    [Test]
    public async Task StopSessionAsync_WhenReasonIsGiven_ShouldShowItInTheStatusLine()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        await vm.StopSessionAsync(reason: "⏸ Stopped after 40 min idle.");

        vm.StatusText.Should().Contain("40 min", "the reason is shown in the status line");
        vm.Status.Should().Be(TrackerStatus.Info);
    }

    /// <summary>
    /// Covers the tracker view model's preview-field behavior. The Azure DevOps
    /// add-in fills these fields; the tracker must consume them for exactly one
    /// session, so the test lives with the tracker rather than the add-in.
    /// </summary>
    [Test]
    public async Task BuildEntry_WhenAPreviewBookingElementIsSet_ShouldUseItForTheNextSessionThenClearIt()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        vm.PreviewBookingElement = "Quarterly figures";

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        (await repo.GetAllAsync()).Single().BookingElement.Should().Be("Quarterly figures");
        vm.PreviewBookingElement.Should().BeEmpty("the preview is consumed by one session");
    }

    [Test]
    public async Task BookGapAsync_WhenATaskIsGiven_ShouldSaveASessionWithTheGapsTimes()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        var gap = new TimeRange(
            new DateTimeOffset(2026, 9, 21, 10, 15, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 21, 11, 5, 0, TimeSpan.FromHours(2)));

        var booked = await vm.BookGapAsync(gap, "  Report ", "Project X");

        booked.Should().BeTrue();
        var session = RepositoryFake.Persisted(repo).Single();
        session.Task.Should().Be("Report");
        session.BookingElement.Should().Be("Project X");
        session.Start.Should().Be(gap.Start);
        session.End.Should().Be(gap.End);
        session.DurationSeconds.Should().Be(3000, "the duration is derived from the times");
        vm.Week.Status.Should().Be(WeekStatus.Success);
    }

    [Test]
    public async Task BookGapAsync_WhenNoBookingElementIsGiven_ShouldInheritTheTasksLatestOne()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", "Quarterly", 9));
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        var gap = new TimeRange(
            new DateTimeOffset(2026, 9, 19, 14, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 19, 15, 0, 0, TimeSpan.FromHours(2)));

        await vm.BookGapAsync(gap, "report", "");

        RepositoryFake.Persisted(repo).Last().BookingElement.Should().Be("Quarterly");
    }

    [Test]
    public async Task BookGapAsync_WhenTheTaskNameIsEmpty_ShouldSaveNothingAndReportIt()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        var gap = new TimeRange(
            new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 21, 11, 0, 0, TimeSpan.FromHours(2)));

        var booked = await vm.BookGapAsync(gap, "   ", "");

        booked.Should().BeFalse();
        RepositoryFake.Persisted(repo).Should().BeEmpty();
        vm.Week.Status.Should().Be(WeekStatus.Error);
    }

    [Test]
    public async Task ApplySessionChangesAsync_WhenAChangeIsApplied_ShouldPersistItAndReload()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9));
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        var original = vm.Week.Sessions.Single();
        var stretched = original.Clone();
        stretched.Reschedule(original.Start, original.End.AddMinutes(30));

        var saved = await vm.ApplySessionChangesAsync([new SessionChange(original, stretched)]);

        saved.Should().BeTrue();
        RepositoryFake.Persisted(repo).Single().DurationSeconds.Should().Be(3600);
        vm.Week.Sessions.Single().End.Should().Be(stretched.End, "the week view is rebuilt from the saved log");
    }

    [Test]
    public async Task ApplySessionChangesAsync_WhenTheChangedSessionIsNotInTheLoadedLog_ShouldSaveNothingAndReportIt()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", 9));
        using var vm = TrackerViewModelFactory.Create(repo);
        await vm.InitialLoad;
        // An equal-looking copy stands for a session from an earlier load: a reload
        // from the file creates new objects, so a plan made before it no longer matches.
        var stale = vm.Week.Sessions.Single().Clone();

        var saved = await vm.ApplySessionChangesAsync([new SessionChange(stale, null)]);

        saved.Should().BeFalse();
        RepositoryFake.Persisted(repo).Should().ContainSingle();
        vm.Week.Status.Should().Be(WeekStatus.Error);
    }

    [Test]
    public void Start_WhenTheTimerStarts_ShouldTellTheWeekViewSoItsTimeIsNotAGap()
    {
        var (repo, _) = RepositoryFake.Create();
        var startedAt = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(2));
        using var vm = TrackerViewModelFactory.Create(repo, now: () => startedAt);

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);

        vm.Week.RunningSince.Should().Be(startedAt);
    }

    private static TrackerEntry Entry(string task, int hour) => new()
    {
        Task = task,
        Start = new DateTimeOffset(2026, 9, 19, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 19, hour, 30, 0, TimeSpan.FromHours(2)),
        Duration = "00:30:00",
        DurationSeconds = 1800,
    };

    private static TrackerEntry Entry(string task, string bookingElement, int hour) => new()
    {
        Task = task,
        BookingElement = bookingElement,
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
