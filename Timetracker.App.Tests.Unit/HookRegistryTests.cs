using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

/// <summary>
/// The composition root must expose the repository and its file migration as one
/// shared instance: startup migrates the file the same object later reads.
/// </summary>
public sealed class HookRegistryTests
{
    [Test]
    public void Repository_and_migration_resolve_to_the_same_instance()
    {
        var services = Timetracker.HookRegistry.BuildServiceProvider();

        var repository = services.GetRequiredService<ITrackerRepository>();
        var migration = services.GetRequiredService<ITrackerFileMigration>();

        migration.Should().BeSameAs(repository,
            "one repository instance owns the file for both reading and migrating");
    }
}
