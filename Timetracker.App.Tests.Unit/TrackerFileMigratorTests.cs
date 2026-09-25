using Timetracker.Interfaces;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

/// <summary>
/// The migrator chains one step per version, so adding a future version means
/// adding one <see cref="ITrackerFileMigration"/> rather than changing the runner.
/// </summary>
[TestFixture]
public sealed class TrackerFileMigratorTests
{
    [Test]
    public void MigrateIfNeeded_WhenTheFileIsOlder_ShouldChainStepsUpToTheTarget()
    {
        var oneToTwo = new StubMigration(1, 2);
        var twoToThree = new StubMigration(2, 3);

        var chain = TrackerFileMigrator.BuildChain(1, 3, [twoToThree, oneToTwo]);

        chain.Should().ContainInOrder(oneToTwo, twoToThree);
    }

    [Test]
    public void MigrateIfNeeded_WhenTheFileIsUpToDate_ShouldNeedNoSteps()
    {
        var chain = TrackerFileMigrator.BuildChain(2, 2, [new StubMigration(1, 2)]);

        chain.Should().BeEmpty();
    }

    [Test]
    public void MigrateIfNeeded_WhenAStepIsMissing_ShouldReportIt()
    {
        var act = () => TrackerFileMigrator.BuildChain(1, 3, [new StubMigration(1, 2)]);

        act.Should().Throw<System.Text.Json.JsonException>()
            .WithMessage("*from version 2*");
    }

    [Test]
    public void MigrateIfNeeded_WhenTheFileIsVersionOne_ShouldUpgradeItToVersionTwo()
    {
        var path = TempPath();
        WriteVersionOne(path, """
            [
              { "task": "Report", "bookingElement": "", "start": "2026-09-18T09:00:00+02:00", "end": "2026-09-18T09:30:00+02:00", "duration": "00:30:00", "durationSeconds": 1800 },
              { "task": "Report", "bookingElement": "Quarterly", "start": "2026-09-18T14:00:00+02:00", "end": "2026-09-18T15:00:00+02:00", "duration": "01:00:00", "durationSeconds": 3600 },
              { "task": "Meeting", "bookingElement": "Project X", "start": "2026-09-18T11:00:00+02:00", "end": "2026-09-18T12:00:00+02:00", "duration": "01:00:00", "durationSeconds": 3600 }
            ]
            """);
        var repo = new JsonTrackerRepository(path);
        var migrator = MigratorFor(path);

        var migrated = migrator.MigrateIfNeeded();

        migrated.Should().BeTrue();
        var text = File.ReadAllText(path);
        text.Should().Contain("\"version\": 2");
        text.Should().Contain("\"name\": \"Report\"");
        text.Should().Contain("\"sessions\"");
        text.Split("\"name\":").Length.Should().Be(3, "Report and Meeting are two records");

        // The data survives the migration, grouped as asked.
        var all = repo.GetAll();
        all.Should().HaveCount(3);
        all.Where(e => e.Task == "Report").Should().OnlyContain(e => e.BookingElement == "Quarterly",
            "the task takes the first non-empty booking element");
        all.Single(e => e.Task == "Meeting").BookingElement.Should().Be("Project X");
    }

    [Test]
    public void MigrateIfNeeded_WhenItRuns_ShouldBackUpTheOriginalFileBeforeRewritingIt()
    {
        var path = TempPath();
        var original = """
            [
              { "task": "Report", "bookingElement": "Quarterly", "start": "2026-09-18T09:00:00+02:00", "end": "2026-09-18T09:30:00+02:00", "duration": "00:30:00", "durationSeconds": 1800 }
            ]
            """;
        WriteVersionOne(path, original);
        var migrator = MigratorFor(path);

        migrator.MigrateIfNeeded();

        File.Exists(BackupPath(path)).Should().BeTrue("the original is kept");
        File.ReadAllText(BackupPath(path)).Should().Be(original,
            "the backup is the untouched version-1 file");
        File.ReadAllText(path).Should().Contain("\"version\": 2");
    }

    [Test]
    public void MigrateIfNeeded_WhenThereIsNothingToMigrate_ShouldNotBackUpTheFile()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));
        var migrator = MigratorFor(path);

        migrator.MigrateIfNeeded();

        File.Exists(BackupPath(path)).Should().BeFalse("no migration happened");
    }

    [Test]
    public void MigrateIfNeeded_WhenTheFileIsAlreadyVersionTwo_ShouldLeaveItUntouched()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));
        var before = File.ReadAllText(path);
        var migrator = MigratorFor(path);

        var migrated = migrator.MigrateIfNeeded();

        migrated.Should().BeFalse("the file is already version 2");
        File.ReadAllText(path).Should().Be(before);
    }

    [Test]
    public void MigrateIfNeeded_WhenThereIsNoFile_ShouldDoNothing()
    {
        var path = TempPath();
        var migrator = MigratorFor(path);

        migrator.MigrateIfNeeded().Should().BeFalse();
        File.Exists(BackupPath(path)).Should().BeFalse();
    }

    [Test]
    public void MigrateIfNeeded_WhenTheFileIsUnreadable_ShouldKeepItAndNotThrow()
    {
        var path = TempPath();
        WriteVersionOne(path, "{ not valid json");
        var migrator = MigratorFor(path);

        var migrated = migrator.MigrateIfNeeded();

        migrated.Should().BeFalse("a broken file is left for the normal load path");
        File.ReadAllText(path).Should().Be("{ not valid json");
    }

    [Test]
    public void MigrateIfNeeded_WhenTheFileIsBroken_ShouldReportItThroughTheLog()
    {
        var path = TempPath();
        WriteVersionOne(path, "{ not valid json");
        string? loggedContext = null;
        var migrator = new TrackerFileMigrator(
            path, [new VersionOneToTwoMigration(path)], (context, _) => loggedContext = context);

        migrator.MigrateIfNeeded();

        loggedContext.Should().Be("TrackerFileMigration");
    }

    [Test]
    public void MigrateIfNeeded_WhenTheFileHasAnUnsupportedNewerVersion_ShouldReportItAsCorruptAndNotOverwriteIt()
    {
        var path = TempPath();
        File.WriteAllText(path, """{ "version": 99, "tasks": [] }""");
        var repo = new JsonTrackerRepository(path);

        repo.GetAll().Should().BeEmpty("an unknown version cannot be read");

        // Loading must not have destroyed the unknown file.
        File.ReadAllText(path).Should().Contain("\"version\": 99");
    }

    private static void WriteVersionOne(string path, string json) => File.WriteAllText(path, json);

    /// <summary>A migrator with the production migration set (v1 → current).</summary>
    private static TrackerFileMigrator MigratorFor(string path) =>
        new(path, [new VersionOneToTwoMigration(path)]);

    /// <summary>Where the production migration keeps the pre-migration copy.</summary>
    private static string BackupPath(string path) =>
        new VersionOneToTwoMigration(path).BackupPath;

    private static string TempPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opencode", "tt-repo-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"tt-{Guid.NewGuid():N}.json");
    }

    private static TrackerEntry NewEntry(string task, int hour = 9, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(2026, 9, 18, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 18, hour + 1, 0, 0, TimeSpan.FromHours(2)),
        Duration = "01:00:00",
        DurationSeconds = 3600,
    };

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
