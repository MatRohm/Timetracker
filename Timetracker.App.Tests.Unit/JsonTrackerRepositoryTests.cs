using NUnit.Framework;
using AwesomeAssertions;
using Timetracker.Models;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

public sealed class JsonTrackerRepositoryTests
{
    [Test]
    public void Add_persists_the_entry_to_the_json_file()
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
    public void Add_is_append_only_and_never_removes_entries()
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
    public void Save_rewrites_all_entries_preserving_each_one()
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
    public void File_is_written_with_the_version_marker_and_one_record_per_task()
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
    public void Sessions_of_the_same_task_are_stored_under_one_record()
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
    public void Sessions_are_saved_oldest_first_and_load_in_order()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 14));
        repo.Add(NewEntry("Report", hour: 9));

        var all = repo.GetAll();

        all.Select(e => e.Start.Hour).Should().Equal(9, 14);
    }

    [Test]
    public void The_booking_element_of_a_task_is_the_first_non_empty_one()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 9, bookingElement: ""));
        repo.Add(NewEntry("Report", hour: 14, bookingElement: "Quarterly"));

        var all = repo.GetAll();

        all.Should().OnlyContain(e => e.BookingElement == "Quarterly");
    }

    [Test]
    public void Tasks_differing_only_by_case_are_merged_into_one_record()
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
    public void Missing_booking_element_loads_as_empty_string()
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
    public void Legacy_description_field_is_migrated_to_booking_element()
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
    public void MigrateIfNeeded_upgrades_a_version_one_file_to_version_two()
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

        var migrated = repo.MigrateIfNeeded();

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
    public void MigrateIfNeeded_backs_up_the_original_file_before_rewriting_it()
    {
        var path = TempPath();
        var original = """
            [
              { "task": "Report", "bookingElement": "Quarterly", "start": "2026-09-18T09:00:00+02:00", "end": "2026-09-18T09:30:00+02:00", "duration": "00:30:00", "durationSeconds": 1800 }
            ]
            """;
        WriteVersionOne(path, original);
        var repo = new JsonTrackerRepository(path);

        repo.MigrateIfNeeded();

        File.Exists(repo.MigrationBackupPath).Should().BeTrue("the original is kept");
        File.ReadAllText(repo.MigrationBackupPath).Should().Be(original,
            "the backup is the untouched version-1 file");
        File.ReadAllText(path).Should().Contain("\"version\": 2");
    }

    [Test]
    public void MigrateIfNeeded_does_not_back_up_when_there_is_nothing_to_migrate()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));

        repo.MigrateIfNeeded();

        File.Exists(repo.MigrationBackupPath).Should().BeFalse("no migration happened");
    }

    [Test]
    public void MigrateIfNeeded_leaves_a_version_two_file_untouched()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report"));
        var before = File.ReadAllText(path);

        var migrated = repo.MigrateIfNeeded();

        migrated.Should().BeFalse("the file is already version 2");
        File.ReadAllText(path).Should().Be(before);
    }

    [Test]
    public void MigrateIfNeeded_does_nothing_when_there_is_no_file()
    {
        var repo = new JsonTrackerRepository(TempPath());

        repo.MigrateIfNeeded().Should().BeFalse();
    }

    [Test]
    public void MigrateIfNeeded_keeps_an_unreadable_file_and_does_not_throw()
    {
        var path = TempPath();
        WriteVersionOne(path, "{ not valid json");
        var repo = new JsonTrackerRepository(path);

        var migrated = repo.MigrateIfNeeded();

        migrated.Should().BeFalse("a broken file is left for the normal load path");
        File.ReadAllText(path).Should().Be("{ not valid json");
    }

    [Test]
    public void An_unsupported_newer_version_is_reported_as_corrupt_and_not_overwritten()
    {
        var path = TempPath();
        File.WriteAllText(path, """{ "version": 99, "tasks": [] }""");
        var repo = new JsonTrackerRepository(path);

        repo.GetAll().Should().BeEmpty("an unknown version cannot be read");

        // Loading must not have destroyed the unknown file.
        File.ReadAllText(path).Should().Contain("\"version\": 99");
    }

    [Test]
    public void Save_writes_the_booking_element_under_its_new_name()
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
    public void GetAll_on_a_missing_file_returns_empty_list()
    {
        var all = new JsonTrackerRepository(TempPath()).GetAll();

        all.Should().BeEmpty();
    }

    private static void WriteVersionOne(string path, string json) => File.WriteAllText(path, json);

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
