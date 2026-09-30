using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using static Timetracker.Plugins.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class ActivityOptionsContributorTests
{
    [Test]
    public void Options_WhenCreated_ShouldShowTheActivityAndStateFilesAsReadOnlyPaths()
    {
        var logPath = TempPath($"options-{Guid.NewGuid():N}.json");
        var contributor = new ActivityOptionsContributor(new ActivityLog(logPath));

        var options = contributor.Options;

        options.Select(o => o.DefaultValue).Should().Equal(logPath, ActivityTracker.StateFilePath);
        options.Should().OnlyContain(o => o.Kind == OptionKind.Path && o.IsReadOnly);
        contributor.Section.Should().Be("Activity monitor");
    }
}
