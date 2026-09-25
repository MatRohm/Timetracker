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
    public void TrackerFileMigrator_WhenFileVersionIsOlder_ShouldChainStepsUpToTheTarget()
    {
        var oneToTwo = new StubMigration(1, 2);
        var twoToThree = new StubMigration(2, 3);

        var chain = TrackerFileMigrator.BuildChain(1, 3, [twoToThree, oneToTwo]);

        chain.Should().ContainInOrder(oneToTwo, twoToThree);
    }

    [Test]
    public void TrackerFileMigrator_WhenFileIsUpToDate_ShouldNeedNoSteps()
    {
        var chain = TrackerFileMigrator.BuildChain(2, 2, [new StubMigration(1, 2)]);

        chain.Should().BeEmpty();
    }

    [Test]
    public void TrackerFileMigrator_WhenStepIsMissing_ShouldReportIt()
    {
        var act = () => TrackerFileMigrator.BuildChain(1, 3, [new StubMigration(1, 2)]);

        act.Should().Throw<System.Text.Json.JsonException>()
            .WithMessage("*from version 2*");
    }

    [Test]
    public void TrackerFileMigrator_WhenVersionOneMigration_ShouldStateTheRangeItHandles()
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
