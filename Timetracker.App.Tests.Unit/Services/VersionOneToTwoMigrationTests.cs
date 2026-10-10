using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Models;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

/// <summary>The version-1 step describes the range it migrates and where it backs up.</summary>
[TestFixture]
public sealed class VersionOneToTwoMigrationTests
{
    [Test]
    public void FromVersion_WhenAsked_ShouldStateTheRangeItHandles()
    {
        var migration = new VersionOneToTwoMigration("unused.json");

        migration.FromVersion.Should().Be(TrackerDocument.UnversionedVersion);
        migration.ToVersion.Should().Be(2);
        migration.ToVersion.Should().Be(migration.FromVersion + 1, "a step moves exactly one version");
        migration.BackupPath.Should().Be("unused.json.v1-backup");
    }
}
