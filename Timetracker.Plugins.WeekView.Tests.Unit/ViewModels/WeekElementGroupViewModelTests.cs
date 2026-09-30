using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Tests.Unit.ViewModels;

[TestFixture]
public sealed class WeekElementGroupViewModelTests
{
    [Test]
    public void TotalText_WhenItHasEntries_ShouldSumTheirDurations()
    {
        var group = new WeekElementGroupViewModel("Project X",
        [
            new WeekEntryViewModel("Report", 3600),
            new WeekEntryViewModel("Review", 1800),
        ]);

        group.TotalText.Should().Be("1:30");
    }

    [Test]
    public void CopyText_WhenItHasEntries_ShouldListTheTaskNamesOnePerLine()
    {
        var group = new WeekElementGroupViewModel("Project X",
        [
            new WeekEntryViewModel("Report", 3600),
            new WeekEntryViewModel("Review", 1800),
        ]);

        group.CopyText.Should().Be("Report" + Environment.NewLine + "Review");
    }

    [Test]
    public void IsExpanded_WhenTheViewModelIsCreated_ShouldBeFalse()
    {
        var group = new WeekElementGroupViewModel("Project X", []);

        group.IsExpanded.Should().BeFalse("nodes start collapsed");
    }

    [Test]
    public void ToggleCommand_WhenExecuted_ShouldToggleTheGroup()
    {
        var group = new WeekElementGroupViewModel("Project X", []);

        group.ToggleCommand.Execute(null);
        group.IsExpanded.Should().BeTrue();

        group.ToggleCommand.Execute(null);
        group.IsExpanded.Should().BeFalse();
    }
}
