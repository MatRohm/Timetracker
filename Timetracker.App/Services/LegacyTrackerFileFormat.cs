using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>Groups entries into the legacy version-2 shape, mirroring <see cref="TrackerFileFormat"/>.</summary>
internal static class LegacyTrackerFileFormat
{
    public static LegacyTrackerDocument ToDocument(IEnumerable<TrackerEntry> entries, int version)
    {
        var tasks = entries
            .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new LegacyTrackedTask
            {
                Name = group.Key,
                BookingElement = group.Select(e => e.BookingElement)
                    .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "",
                Sessions = [.. group
                    .OrderBy(e => e.Start)
                    .Select(e => new LegacySessionSpan
                    {
                        Id = e.Id,
                        Start = e.Start,
                        End = e.End,
                        Duration = e.Duration,
                        DurationSeconds = e.DurationSeconds,
                    })],
            })
            .ToList();

        return new LegacyTrackerDocument { Version = version, Tasks = tasks };
    }
}
