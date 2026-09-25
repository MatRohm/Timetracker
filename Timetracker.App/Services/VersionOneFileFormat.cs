using System.Text.Json;
using Timetracker.Models;

namespace Timetracker.Services;

/// <summary>
/// Reads a version-1 tracker file: the unversioned flat array of one record per
/// session. Version 1 had no version marker, so the array shape itself identifies
/// it. Older entries may store the booking element under the JSON name
/// "description"; that value is mapped to <see cref="TrackerEntry.BookingElement"/>
/// so existing data survives the rename.
/// </summary>
internal static class VersionOneFileFormat
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The entries of a version-1 array, preserving their file order.</summary>
    public static List<TrackerEntry> Read(JsonElement root)
    {
        var sessions = root.Deserialize<List<SessionDto>>(JsonOptions) ?? [];
        return [.. sessions.Select(ToEntry)];
    }

    private static TrackerEntry ToEntry(SessionDto dto) => new()
    {
        Task = dto.Task,
        BookingElement = dto.BookingElement.Length > 0 ? dto.BookingElement : dto.Description,
        Start = dto.Start,
        End = dto.End,
        Duration = dto.Duration,
        DurationSeconds = dto.DurationSeconds,
    };

    /// <summary>Load shape of a version-1 entry, tolerating both JSON field names.</summary>
    private sealed class SessionDto
    {
        public string Task { get; set; } = "";
        public string Description { get; set; } = "";
        public string BookingElement { get; set; } = "";
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public string Duration { get; set; } = "";
        public double DurationSeconds { get; set; }
    }
}
