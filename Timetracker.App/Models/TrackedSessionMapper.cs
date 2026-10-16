using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Models;

/// <summary>
/// Maps the app's mutable <see cref="TrackerEntry"/> to the contract's read-only
/// <see cref="TrackedSession"/>. Shared by the session host and the tracking state
/// machine so the conversion lives in one place.
/// </summary>
internal static class TrackedSessionMapper
{
    internal static TrackedSession ToTracked(TrackerEntry entry) => new()
    {
        Id = entry.Id,
        Task = entry.Task,
        BookingElement = entry.BookingElement,
        Start = entry.Start,
        End = entry.End,
        Duration = entry.Duration,
        DurationSeconds = entry.DurationSeconds,
    };
}
