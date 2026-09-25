using System.Text.Encodings.Web;
using System.Text.Json;
using Timetracker.Models;

namespace Timetracker.Services;

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
        Name = group.Key,
        BookingElement = FirstBookingElement(group),
        Sessions = [.. group
            .OrderBy(e => e.Start)
            .Select(e => new TrackedSession
            {
                Start = e.Start,
                End = e.End,
                Duration = e.Duration,
                DurationSeconds = e.DurationSeconds,
            })],
    };

    private static IEnumerable<TrackerEntry> BuildEntries(TrackedTask task) =>
        task.Sessions.Select(session => new TrackerEntry
        {
            Task = task.Name,
            BookingElement = task.BookingElement,
            Start = session.Start,
            End = session.End,
            Duration = session.Duration,
            DurationSeconds = session.DurationSeconds,
        });

    private static string FirstBookingElement(IEnumerable<TrackerEntry> group) =>
        group.Select(e => e.BookingElement)
            .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";
}
