using NUnit.Framework;
using AwesomeAssertions;
using Timetracker.Models;
using Timetracker.Services;

namespace Timetracker.Tests.Unit;

[TestFixture]
public sealed class JsonTrackerRepositoryTests
{
    [Test]
    public void Add_WhenAnEntryIsAdded_ShouldPersistItToTheJsonFile()
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
    public void Add_WhenEntriesAreAdded_ShouldNeverRemoveExistingOnes()
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
    public void Save_WhenCalled_ShouldRewriteAllEntriesPreservingEachOne()
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
    public void Save_WhenCalled_ShouldWriteTheVersionMarkerAndOneRecordPerTask()
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
    public void Save_WhenSessionsBelongToTheSameTask_ShouldStoreThemUnderOneRecord()
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
    public void Save_WhenSessionsAreSaved_ShouldStoreThemOldestFirst()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 14));
        repo.Add(NewEntry("Report", hour: 9));

        var all = repo.GetAll();

        all.Select(e => e.Start.Hour).Should().Equal(9, 14);
    }

    [Test]
    public void Save_WhenATaskHasSeveralBookingElements_ShouldUseTheFirstNonEmptyOne()
    {
        var path = TempPath();
        var repo = new JsonTrackerRepository(path);
        repo.Add(NewEntry("Report", hour: 9, bookingElement: ""));
        repo.Add(NewEntry("Report", hour: 14, bookingElement: "Quarterly"));

        var all = repo.GetAll();

        all.Should().OnlyContain(e => e.BookingElement == "Quarterly");
    }

    [Test]
    public void Save_WhenTasksDifferOnlyByCase_ShouldMergeThemIntoOneRecord()
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
    public void GetAll_WhenTheBookingElementIsMissing_ShouldReturnItEmpty()
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
    public void GetAll_WhenTheLegacyDescriptionFieldIsPresent_ShouldMapItToTheBookingElement()
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
    public void Save_WhenCalled_ShouldWriteTheBookingElementUnderItsNewName()
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
    public void GetAll_WhenTheFileIsMissing_ShouldReturnAnEmptyList()
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
