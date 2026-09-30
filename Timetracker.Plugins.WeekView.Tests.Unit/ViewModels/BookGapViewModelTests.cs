using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Tests.Unit.ViewModels;

[TestFixture]
public sealed class BookGapViewModelTests
{
    private static readonly TimeRange Gap = new(
        new DateTimeOffset(2026, 9, 21, 10, 15, 0, TimeSpan.FromHours(2)),
        new DateTimeOffset(2026, 9, 21, 11, 5, 0, TimeSpan.FromHours(2)));

    [Test]
    public void Times_WhenTheDialogOpens_ShouldShowTheGapsStartEndAndDuration()
    {
        var booking = new BookGapViewModel(Gap, []);

        booking.Times.StartText.Should().Be("2026-09-21 10:15");
        booking.Times.EndText.Should().Be("2026-09-21 11:05");
        booking.Times.DurationText.Should().Be("00:50:00");
    }

    [Test]
    public void CanBook_WhenNoTaskNameIsEntered_ShouldBeFalse()
    {
        var booking = new BookGapViewModel(Gap, []) { TaskName = "   " };

        booking.CanBook.Should().BeFalse();
    }

    [Test]
    public void CanBook_WhenTheEndLiesBeforeTheStart_ShouldBeFalse()
    {
        var booking = new BookGapViewModel(Gap, []) { TaskName = "Report" };

        booking.Times.EndText = "2026-09-21 10:00";

        booking.CanBook.Should().BeFalse();
    }

    [Test]
    public void Range_WhenTheTimesAreEdited_ShouldFollowThem()
    {
        var booking = new BookGapViewModel(Gap, []) { TaskName = "Report" };

        booking.Times.StartText = "2026-09-21 10:30";

        booking.CanBook.Should().BeTrue();
        booking.Range.Should().Be(new TimeRange(Gap.Start.AddMinutes(15), Gap.End));
    }

    [Test]
    public void TaskName_WhenTyped_ShouldSuggestKnownTasks()
    {
        var booking = new BookGapViewModel(Gap, [new TrackedSession { Task = "Quarterly report", DurationSeconds = 600 }]);

        booking.TaskName = "report";

        booking.Suggestions.Suggestions.Select(s => s.Name).Should().Equal("Quarterly report");
    }

    [Test]
    public void AcceptSuggestion_WhenASuggestionIsPicked_ShouldTakeItsNameAndHideTheList()
    {
        var booking = new BookGapViewModel(Gap, [new TrackedSession { Task = "Quarterly report", DurationSeconds = 600 }]);
        booking.TaskName = "report";

        booking.AcceptSuggestion(booking.Suggestions.Suggestions.Single());

        booking.TaskName.Should().Be("Quarterly report");
        booking.Suggestions.ShowSuggestions.Should().BeFalse();
    }
}
