using NUnit.Framework;
using AwesomeAssertions;
using Timetracker.Models;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

public sealed class JsonTrackerRepositoryTests
{
    [Test]
    public void JsonTrackerRepository_WhenEntryIsAdded_ShouldPersistItToTheJsonFile()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);

        repo.Add(new TrackerEntry
        {
            Task = "Report",
            Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
            End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
            Duration = "01:00:00",
            DurationSeconds = 3600,
            BookingElement = "quarterly",
        });

        File.Exists(path).Should().BeTrue();
        var all = new JsonTrackerRepository(path).GetAll();
        all.Should().ContainSingle();
        all[0].Task.Should().Be("Report");
        all[0].BookingElement.Should().Be("quarterly");
        all[0].DurationSeconds.Should().Be(3600);
    }

    [Test]
    public void JsonTrackerRepository_WhenEntriesAreAdded_ShouldNeverRemoveExistingEntries()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("First"));
        repo.Add(NewEntry("Second"));
        repo.Add(NewEntry("Third"));

        var all = repo.GetAll();

        all.Select(e => e.Task).Should().Equal("First", "Second", "Third");
    }

    [Test]
    public void JsonTrackerRepository_WhenSaveIsCalled_ShouldRewriteAllEntriesPreservingEachOne()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("First"));
        repo.Add(NewEntry("Second"));

        var entries = repo.GetAll().ToList();
        entries[0].Task = "Renamed";
        repo.Save(entries);

        var all = repo.GetAll();
        all.Should().HaveCount(2, "editing must never delete entries");
        all[0].Task.Should().Be("Renamed");
        all[1].Task.Should().Be("Second");
    }

    [Test]
    public void JsonTrackerRepository_WhenFileIsWritten_ShouldIncludeTheVersionMarkerAndOneRecordPerTask()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", bookingElement: "Quarterly"));
        repo.Add(NewEntry("Report", bookingElement: "Quarterly"));

        var text = File.ReadAllText(path);

        text.Should().Contain("\"version\": 2");
        text.Should().Contain("\"name\": \"Report\"");
        text.Should().Contain("\"sessions\"");
        // One record for the task, not one per session.
        text.Split("\"name\":").Length.Should().Be(2, "both sessions belong to the same task record");
    }

    [Test]
    public void JsonTrackerRepository_WhenSessionsBelongToTheSameTask_ShouldStoreThemUnderOneRecord()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 9));
        repo.Add(NewEntry("Meeting", hour: 11));
        repo.Add(NewEntry("Report", hour: 14));

        var all = repo.GetAll();

        all.Should().HaveCount(3);
        all.Count(e => e.Task == "Report").Should().Be(2);
    }

    [Test]
    public void JsonTrackerRepository_WhenSessionsAreSaved_ShouldSaveOldestFirstAndLoadInOrder()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 14));
        repo.Add(NewEntry("Report", hour: 9));

        var all = repo.GetAll();

        all.Select(e => e.Start.Hour).Should().Equal(9, 14);
    }

    [Test]
    public void JsonTrackerRepository_WhenTaskHasMultipleBookingElements_ShouldUseTheFirstNonEmptyOne()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 9, bookingElement: ""));
        repo.Add(NewEntry("Report", hour: 14, bookingElement: "Quarterly"));

        var all = repo.GetAll();

        all.Should().OnlyContain(e => e.BookingElement == "Quarterly");
    }

    [Test]
    public void JsonTrackerRepository_WhenTasksDifferOnlyByCase_ShouldMergeThemIntoOneRecord()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));
        repo.Add(NewEntry("report"));

        File.ReadAllText(path).Split("\"name\":").Length.Should().Be(2,
            "the two spellings are one record");

        var all = repo.GetAll();
        all.Should().HaveCount(2);
        all.Should().OnlyContain(e => e.Task == "Report", "the first spelling is kept");
    }

    [Test]
    public void JsonTrackerRepository_WhenBookingElementIsMissing_ShouldLoadItAsEmptyString()
    {
        var path = TempPath();
        WriteVersionOne(path, """
            [
              { "task": "Legacy", "start": "2026-09-18T09:00:00+02:00", "end": "2026-09-18T10:00:00+02:00", "duration": "01:00:00", "durationSeconds": 3600 }
            ]
            """);

        var all = new JsonTrackerRepository(path).GetAll();

        all.Should().ContainSingle().Which.BookingElement.Should().BeEmpty();
    }

    [Test]
    public void JsonTrackerRepository_WhenLegacyDescriptionFieldIsPresent_ShouldMigrateItToBookingElement()
    {
        var path = TempPath();
        WriteVersionOne(path, """
            [
              { "task": "Legacy", "description": "old note", "start": "2026-09-18T09:00:00+02:00", "end": "2026-09-18T10:00:00+02:00", "duration": "01:00:00", "durationSeconds": 3600 },
              { "task": "Current", "bookingElement": "new note", "start": "2026-09-18T11:00:00+02:00", "end": "2026-09-18T12:00:00+02:00", "duration": "01:00:00", "durationSeconds": 3600 }
            ]
            """);

        var all = new JsonTrackerRepository(path).GetAll();

        all[0].BookingElement.Should().Be("old note", "the legacy JSON name is migrated");
        all[1].BookingElement.Should().Be("new note", "the current JSON name is preferred");
    }

    [Test]
    public void JsonTrackerRepository_WhenVersionOneFileIsMigrated_ShouldUpgradeItToVersionTwo()
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
    public void JsonTrackerRepository_WhenMigrationRuns_ShouldBackUpTheOriginalFileBeforeRewritingIt()
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
    public void JsonTrackerRepository_WhenThereIsNothingToMigrate_ShouldNotBackUpTheFile()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));
        var migrator = MigratorFor(path);

        migrator.MigrateIfNeeded();

        File.Exists(BackupPath(path)).Should().BeFalse("no migration happened");
    }

    [Test]
    public void JsonTrackerRepository_WhenFileIsVersionTwoAndMigrationRuns_ShouldLeaveItUntouched()
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
    public void JsonTrackerRepository_WhenThereIsNoFile_ShouldDoNothing()
    {
        var path = TempPath();
        var migrator = MigratorFor(path);

        migrator.MigrateIfNeeded().Should().BeFalse();
        File.Exists(BackupPath(path)).Should().BeFalse();
    }

    [Test]
    public void JsonTrackerRepository_WhenFileIsUnreadable_ShouldKeepItAndNotThrow()
    {
        var path = TempPath();
        WriteVersionOne(path, "{ not valid json");
        var migrator = MigratorFor(path);

        var migrated = migrator.MigrateIfNeeded();

        migrated.Should().BeFalse("a broken file is left for the normal load path");
        File.ReadAllText(path).Should().Be("{ not valid json");
    }

    [Test]
    public void JsonTrackerRepository_WhenFileIsBroken_ShouldReportItThroughTheLog()
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
    public void JsonTrackerRepository_WhenFileHasUnsupportedNewerVersion_ShouldReportItAsCorruptAndNotOverwriteIt()
    {
        var path = TempPath();
        File.WriteAllText(path, """{ "version": 99, "tasks": [] }""");
        var repo = new JsonTrackerRepository(path);

        repo.GetAll().Should().BeEmpty("an unknown version cannot be read");

        // Loading must not have destroyed the unknown file.
        File.ReadAllText(path).Should().Contain("\"version\": 99");
    }

    [Test]
    public void JsonTrackerRepository_WhenSaveIsCalled_ShouldWriteTheBookingElementUnderItsNewName()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));

        repo.Save(repo.GetAll());

        var text = File.ReadAllText(path);
        text.Should().Contain("\"bookingElement\"");
        text.Should().NotContain("description");
    }

    [Test]
    public void JsonTrackerRepository_WhenFileIsMissing_ShouldReturnAnEmptyList()
    {
        var all = new JsonTrackerRepository(TempPath()).GetAll();

        all.Should().BeEmpty();
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
}
