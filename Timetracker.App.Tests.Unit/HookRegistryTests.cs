using Timetracker.App.Interfaces;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Timetracker.App.Services;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.App.Tests.Unit;

/// <summary>
/// The composition root must register the migration step and its runner against the
/// repository's file, with the runner able to reach every step, and the options
/// store with every component's options section.
/// </summary>
[TestFixture]
public sealed class HookRegistryTests
{
    [Test]
    public void BuildServiceProvider_WhenCalled_ShouldRegisterTheMigratorRunnerAndSteps()
    {
        var services = Timetracker.App.HookRegistry.BuildServiceProvider(NullLoggerFactory.Instance);

        var repository = services.GetRequiredService<ITrackerRepository>();
        var runner = services.GetRequiredService<ITrackerFileMigrationRunner>();
        var steps = services.GetServices<ITrackerFileMigration>();

        repository.Should().BeOfType<JsonTrackerRepository>();
        runner.Should().BeOfType<TrackerFileMigrator>();
        steps.Should().HaveCount(2);
        steps.Should().Contain(s => s is VersionOneToTwoMigration);
        steps.Should().Contain(s => s is VersionTwoToThreeMigration);
    }

    [Test]
    public void BuildServiceProvider_WhenCalled_ShouldRegisterTheOptionsSectionsWithGeneralFirst()
    {
        var services = Timetracker.App.HookRegistry.BuildServiceProvider(NullLoggerFactory.Instance);

        var store = services.GetRequiredService<IOptionQuery>();
        var sections = services.GetServices<IOptionDefinitionQuery>().Select(c => c.Section);

        store.Should().BeOfType<JsonOptionsStore>();
        sections.Should().Equal("General", "Azure DevOps", "Activity monitor");
    }

    [Test]
    public void BuildServiceProvider_WhenCalled_ShouldContributeTheMonitorButtonsToTheOptionsView()
    {
        var services = Timetracker.App.HookRegistry.BuildServiceProvider(NullLoggerFactory.Instance);

        var optionUiSections = services.GetServices<IOptionUiQuery>().Select(c => c.Section);
        optionUiSections.Should().Equal("Activity monitor");

        // The week view no longer carries the monitor; only Azure DevOps targets a tab.
        var tabContributors = services.GetServices<IUiQuery>().Select(c => c.TargetTab);
        tabContributors.Should().Equal("Tracker");
    }
}
