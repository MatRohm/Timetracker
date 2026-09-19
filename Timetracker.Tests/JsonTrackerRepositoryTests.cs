using NUnit.Framework;
using AwesomeAssertions;
using Timetracker.Models;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Tests;

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
    public void Missing_booking_element_loads_as_empty_string()
    {
        var path = TempPath();
        File.WriteAllText(path, """
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
        File.WriteAllText(path, """
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

    private static string TempPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opencode", "tt-repo-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"tt-{Guid.NewGuid():N}.json");
    }

    private static TrackerEntry NewEntry(string task) => new()
    {
        Task = task,
        Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(2)),
        Duration = "01:00:00",
        DurationSeconds = 3600,
    };
}
