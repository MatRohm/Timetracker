using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Tests.Unit.ViewModels;

[TestFixture]
public sealed class CalendarElementGroupViewModelTests
{
    [Test]
    public void TotalText_WhenItHasEntries_ShouldSumTheirDurations()
    {
        var group = new CalendarElementGroupViewModel("Project X",
        [
            new CalendarEntryViewModel("Report", 3600),
            new CalendarEntryViewModel("Review", 1800),
        ]);

        group.TotalText.Should().Be("1:30");
    }

    [Test]
    public void CopyText_WhenItHasEntries_ShouldListTheTaskNamesOnePerLine()
    {
        var group = new CalendarElementGroupViewModel("Project X",
        [
            new CalendarEntryViewModel("Report", 3600),
            new CalendarEntryViewModel("Review", 1800),
        ]);

        group.CopyText.Should().Be("Report" + Environment.NewLine + "Review");
    }

    [Test]
    public void IsExpanded_WhenTheViewModelIsCreated_ShouldBeFalse()
    {
        var group = new CalendarElementGroupViewModel("Project X", []);

        group.IsExpanded.Should().BeFalse("nodes start collapsed");
    }

    [Test]
    public void ToggleCommand_WhenExecuted_ShouldToggleTheGroup()
    {
        var group = new CalendarElementGroupViewModel("Project X", []);

        group.ToggleCommand.Execute(null);
        group.IsExpanded.Should().BeTrue();

        group.ToggleCommand.Execute(null);
        group.IsExpanded.Should().BeFalse();
    }
}
