using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

/// <summary>
/// The composition root must register the migration step and its runner against the
/// repository's file, with the runner able to reach every step.
/// </summary>
public sealed class HookRegistryTests
{
    [Test]
    public void The_migrator_runner_and_steps_are_registered()
    {
        var services = Timetracker.HookRegistry.BuildServiceProvider();

        var repository = services.GetRequiredService<ITrackerRepository>();
        var runner = services.GetRequiredService<ITrackerFileMigrationRunner>();
        var steps = services.GetServices<ITrackerFileMigration>();

        repository.Should().BeOfType<JsonTrackerRepository>();
        runner.Should().BeOfType<TrackerFileMigrator>();
        steps.Should().ContainSingle().Which.Should().BeOfType<VersionOneToTwoMigration>();
    }
}
