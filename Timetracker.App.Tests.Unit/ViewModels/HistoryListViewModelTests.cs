using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Tests.Unit.ViewModels;

/// <summary>
/// History list: grouping sessions into one row per task, sorting (with the
/// BookingElement column ignored), per-column filtering and paging over the
/// filtered rows.
/// </summary>
[TestFixture]
public sealed class HistoryListViewModelTests
{
    [Test]
    public void SetSessions_WhenATaskHasMultipleSessions_ShouldShowLatestTimesAndSummedDuration()
    {
        var vm = Create(
            Entry("Report", 9, 60),
            Entry("Report", 14, 30));

        var row = vm.Entries.Single();

        row.StartText.Should().Be("2026-09-18 14:00", "Started shows the latest session");
        row.EndText.Should().Be("2026-09-18 14:30", "Ended shows the latest session");
        row.Duration.Should().Be("01:30:00", "Duration sums all sessions");
    }

    [Test]
    public void SetSessions_WhenThereAreMoreThanTenTasks_ShouldShowTheFirstPageOnly()
    {
        var vm = Create(SeedTasks(25));

        vm.Entries.Should().HaveCount(10);
        vm.TotalPages.Should().Be(3);
        vm.HasMultiplePages.Should().BeTrue();
        vm.CurrentPage.Should().Be(1);
    }

    [Test]
    public void TotalPages_WhenThereAreTenTasksOrFewer_ShouldBeOne()
    {
        var vm = Create(SeedTasks(10));

        vm.Entries.Should().HaveCount(10);
        vm.TotalPages.Should().Be(1);
        vm.HasMultiplePages.Should().BeFalse();
        vm.NextPageCommand.CanExecute(null).Should().BeFalse();
        vm.PreviousPageCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void NextPageCommand_WhenPagingBackAndForth_ShouldCoverAllRows()
    {
        var vm = Create(SeedTasks(25));

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
    public void ApplySort_WhenTheBookingElementColumnIsChosen_ShouldIgnoreIt()
    {
        var vm = Create(Entry("Report", 9, 30));

        vm.ApplySort(nameof(EntryRow.BookingElement));

        vm.SortColumn.Should().NotBe(nameof(EntryRow.BookingElement));
        vm.SortColumn.Should().Be(nameof(EntryRow.StartText), "the previous sort is kept");
    }

    [Test]
    public void ApplySort_WhenTheSameColumnIsChosenTwice_ShouldToggleDirection()
    {
        var vm = Create(Entry("Report", 9, 30));

        vm.ApplySort(nameof(EntryRow.Task));
        vm.SortAscending.Should().BeTrue();

        vm.ApplySort(nameof(EntryRow.Task));
        vm.SortAscending.Should().BeFalse();
    }

    [Test]
    public void SetColumnFilter_WhenTheTaskColumnMatches_ShouldShowOnlyThoseRows()
    {
        var vm = Create(Entry("Report", 9, 30), Entry("Review", 11, 30), Entry("Meeting", 14, 30));

        vm.SetColumnFilter(nameof(EntryRow.Task), "re");

        vm.Entries.Select(r => r.Task).Should().BeEquivalentTo("Report", "Review");
    }

    [Test]
    public void SetColumnFilter_WhenTheBookingElementColumnMatches_ShouldShowOnlyThoseRows()
    {
        var vm = Create(
            Entry("Report", "Quarterly", 9), Entry("Meeting", "Project X", 11));

        vm.SetColumnFilter(nameof(EntryRow.BookingElement), "project");

        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting",
            "the booking element filter only compares the booking element column");
    }

    [Test]
    public void SetColumnFilter_WhenTheStartedColumnMatches_ShouldFilterByDate()
    {
        var vm = Create(Entry("Report", 9, 30), Entry("Meeting", 14, 30));

        vm.SetColumnFilter(nameof(EntryRow.StartText), "14:00");

        vm.Entries.Should().ContainSingle().Which.Task.Should().Be("Meeting");
    }

    [Test]
    public void SetColumnFilter_WhenSeveralColumnsAreFilled_ShouldRequireAllOfThemToMatch()
    {
        var vm = Create(
            Entry("Report", "Quarterly", 9),
            Entry("Report", "Project X", 11),
            Entry("Meeting", "Quarterly", 14));

        vm.SetColumnFilter(nameof(EntryRow.Task), "report");
        vm.SetColumnFilter(nameof(EntryRow.BookingElement), "project");

        vm.Entries.Should().ContainSingle("only the Report+Project row matches both filters");
        vm.Entries[0].BookingElement.Should().Be("Project X");
    }

    [Test]
    public void SetColumnFilter_WhenCleared_ShouldShowEverythingAgain()
    {
        var vm = Create(Entry("Report", 9, 30), Entry("Meeting", 14, 30));
        vm.SetColumnFilter(nameof(EntryRow.Task), "Report");
        vm.Entries.Should().ContainSingle();

        vm.SetColumnFilter(nameof(EntryRow.Task), "");

        vm.Entries.Should().HaveCount(2);
    }

    [Test]
    public void SetColumnFilter_WhenNothingMatches_ShouldShowNoRowsAndASinglePage()
    {
        var vm = Create(Entry("Report", 9, 30));

        vm.SetColumnFilter(nameof(EntryRow.Task), "does-not-exist");

        vm.Entries.Should().BeEmpty();
        vm.TotalPages.Should().Be(1);
        vm.HasMultiplePages.Should().BeFalse();
    }

    [Test]
    public void SetColumnFilter_WhenItMatchesMoreThanAPage_ShouldPageTheFilteredRows()
    {
        // 25 tasks named "Task NN"; "Task" matches all of them, so paging still applies.
        var vm = Create(SeedTasks(25));

        vm.SetColumnFilter(nameof(EntryRow.Task), "Task");

        vm.Entries.Should().HaveCount(10, "the filter spans all pages but only one page is shown");
        vm.TotalPages.Should().Be(3, "25 filtered rows need three pages");
        vm.NextPageCommand.Execute(null);
        vm.CurrentPage.Should().Be(2, "paging works within the filtered rows");
    }

    [Test]
    public void IsColumnFiltered_WhenThatColumnHasAFilter_ShouldBeTrue()
    {
        var vm = Create(Entry("Report", 9, 30));

        vm.IsFilterActive.Should().BeFalse("no filter is set initially");
        vm.IsColumnFiltered(nameof(EntryRow.Task)).Should().BeFalse();

        vm.SetColumnFilter(nameof(EntryRow.Task), "rep");

        vm.IsFilterActive.Should().BeTrue();
        vm.IsColumnFiltered(nameof(EntryRow.Task)).Should().BeTrue();
        vm.IsColumnFiltered(nameof(EntryRow.BookingElement)).Should().BeFalse("only Task is filtered");

        vm.SetColumnFilter(nameof(EntryRow.Task), "   ");
        vm.IsFilterActive.Should().BeFalse("whitespace clears the filter");
    }

    private static HistoryListViewModel Create(params TrackerEntry[] sessions)
    {
        var vm = new HistoryListViewModel();
        vm.SetSessions(sessions);
        return vm;
    }

    /// <summary>A session starting at <paramref name="hour"/>:00 lasting the given minutes.</summary>
    private static TrackerEntry Entry(string task, int hour, int durationMinutes) => new()
    {
        Task = task,
        Start = new DateTimeOffset(2026, 9, 18, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 18, hour, 0, 0, TimeSpan.FromHours(2))
            .AddMinutes(durationMinutes),
        Duration = TimeSpan.FromMinutes(durationMinutes).ToString(@"hh\:mm\:ss"),
        DurationSeconds = TimeSpan.FromMinutes(durationMinutes).TotalSeconds,
    };

    private static TrackerEntry Entry(string task, string bookingElement, int hour) => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(2026, 9, 18, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 18, hour, 30, 0, TimeSpan.FromHours(2)),
        Duration = "00:30:00",
        DurationSeconds = 1800,
    };

    private static TrackerEntry[] SeedTasks(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new TrackerEntry
        {
            Task = $"Task {i:00}",
            Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)).AddMinutes(i),
            End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)).AddMinutes(i),
            Duration = "01:00:00",
            DurationSeconds = 3600,
        })];
}
