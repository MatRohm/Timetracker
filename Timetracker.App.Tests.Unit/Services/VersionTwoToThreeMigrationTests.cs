using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

/// <summary>The version-2 step describes the range it migrates and where it backs up.</summary>
[TestFixture]
public sealed class VersionTwoToThreeMigrationTests
{
    [Test]
    public void FromVersion_WhenAsked_ShouldStateTheRangeItHandles()
    {
        var migration = new VersionTwoToThreeMigration("unused.json");

        migration.FromVersion.Should().Be(2);
        migration.ToVersion.Should().Be(3);
        migration.ToVersion.Should().Be(migration.FromVersion + 1, "a step moves exactly one version");
        migration.BackupPath.Should().Be("unused.json.v2-backup");
    }
}
