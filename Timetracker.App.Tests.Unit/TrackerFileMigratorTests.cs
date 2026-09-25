using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

/// <summary>
/// The migrator chains one step per version, so adding a future version means
/// adding one <see cref="ITrackerFileMigration"/> rather than changing the runner.
/// </summary>
public sealed class TrackerFileMigratorTests
{
    [Test]
    public void Chains_steps_from_the_file_version_up_to_the_target()
    {
        var oneToTwo = new StubMigration(1, 2);
        var twoToThree = new StubMigration(2, 3);

        var chain = TrackerFileMigrator.BuildChain(1, 3, [twoToThree, oneToTwo]);

        chain.Should().ContainInOrder(oneToTwo, twoToThree);
    }

    [Test]
    public void An_up_to_date_file_needs_no_steps()
    {
        var chain = TrackerFileMigrator.BuildChain(2, 2, [new StubMigration(1, 2)]);

        chain.Should().BeEmpty();
    }

    [Test]
    public void A_missing_step_is_reported()
    {
        var act = () => TrackerFileMigrator.BuildChain(1, 3, [new StubMigration(1, 2)]);

        act.Should().Throw<System.Text.Json.JsonException>()
            .WithMessage("*from version 2*");
    }

    [Test]
    public void The_version_one_migration_states_the_range_it_handles()
    {
        var migration = new VersionOneToTwoMigration("unused.json");

        migration.FromVersion.Should().Be(TrackerDocument.UnversionedVersion);
        migration.ToVersion.Should().Be(2);
        migration.ToVersion.Should().Be(migration.FromVersion + 1, "a step moves exactly one version");
        migration.BackupPath.Should().Be("unused.json.v1-backup");
    }

    /// <summary>A migration that moves data through untouched, for chain testing.</summary>
    private sealed class StubMigration : ITrackerFileMigration
    {
        public StubMigration(int fromVersion, int toVersion)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
        }

        public int FromVersion { get; }

        public int ToVersion { get; }

        public string BackupPath => $"stub-{FromVersion}-{ToVersion}.bak";

        public TrackerDocument Read(string text) => new();
    }
}
