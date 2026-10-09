using System.Text.Encodings.Web;
using System.Text.Json;
using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>
/// Maps between the in-memory sessions (<see cref="TrackerEntry"/>, one per
/// allocated time) and the versioned file shape (<see cref="TrackerDocument"/>,
/// one record per task holding its sessions). The grouping rule is shared by the
/// version-1 migration and by every save, so both produce the same readable file.
/// </summary>
internal static class TrackerFileFormat
{
    /// <summary>Serializer settings shared by every read and write of the tracker file.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Groups sessions into one record per task name (case-insensitive, first-seen
    /// spelling kept). A task's booking element is the first non-empty one of its
    /// sessions, or empty when none carries one.
    /// </summary>
    public static TrackerDocument ToDocument(IEnumerable<TrackerEntry> entries)
    {
        var tasks = entries
            .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(BuildTask)
            .ToList();

        return new TrackerDocument
        {
            Version = TrackerDocument.CurrentVersion,
            Tasks = tasks,
        };
    }

    /// <summary>Flattens the document back into one session per stored time.</summary>
    public static List<TrackerEntry> ToEntries(TrackerDocument document) =>
        [.. document.Tasks.SelectMany(BuildEntries)];

    private static TrackedTask BuildTask(IGrouping<string, TrackerEntry> group) => new()
    {
        Id = FirstTaskId(group),
        Name = group.Key,
        BookingElement = FirstBookingElement(group),
        Sessions = [.. group
            .OrderBy(e => e.Start)
            .Select(e => new SessionSpan
            {
                Id = e.Id,
                DateStarted = e.Start,
                DurationSeconds = SecondsBetween(e.Start, e.End),
            })],
    };

    private static IEnumerable<TrackerEntry> BuildEntries(TrackedTask task) =>
        task.Sessions.Select(session =>
        {
            var start = session.DateStarted;
            var end = start.AddSeconds(session.DurationSeconds);
            return new TrackerEntry
            {
                Id = session.Id,
                Task = task.Name,
                TaskId = task.Id,
                BookingElement = task.BookingElement,
                Start = start,
                End = end,
                Duration = (end - start).ToString(@"hh\:mm\:ss"),
                DurationSeconds = session.DurationSeconds,
            };
        });

    private static string FirstBookingElement(IEnumerable<TrackerEntry> group) =>
        group.Select(e => e.BookingElement)
            .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";

    private static Guid FirstTaskId(IEnumerable<TrackerEntry> group) =>
        TrackerEntry.OrNew(group.Select(e => e.TaskId).FirstOrDefault(id => id != Guid.Empty));

    /// <summary>The length of a span in seconds, rounded to one decimal and clamped to zero.</summary>
    internal static double SecondsBetween(DateTimeOffset start, DateTimeOffset end)
    {
        var elapsed = end - start;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var result = Math.Round(elapsed.TotalSeconds, 1);
        return result;
    }
}
