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

        var files = contributor.Options.Where(o => o.Kind == OptionKind.Path);
        files.Select(o => o.DefaultValue).Should().Equal(logPath, ActivityTracker.StateFilePath);
        files.Should().OnlyContain(o => o.IsReadOnly);
        contributor.Section.Should().Be("Activity monitor");
    }

    [Test]
    public void Options_WhenCreated_ShouldOfferTheTwoIdleThresholdsAsEditableMinutes()
    {
        var logPath = TempPath($"options-{Guid.NewGuid():N}.json");
        var contributor = new ActivityOptionsContributor(new ActivityLog(logPath));

        var options = contributor.Options;
        options.Should().Contain(o =>
            o.Key == "ActivityMonitor.IdleSpanThreshold" &&
            o.Label == "Idle span threshold (minutes)" &&
            o.Kind == OptionKind.Text &&
            !o.IsReadOnly &&
            o.DefaultValue == "60");
        options.Should().Contain(o =>
            o.Key == "ActivityMonitor.IdleStopThreshold" &&
            o.Label == "Idle stop threshold (minutes)" &&
            o.Kind == OptionKind.Text &&
            !o.IsReadOnly &&
            o.DefaultValue == "30");
    }
}
