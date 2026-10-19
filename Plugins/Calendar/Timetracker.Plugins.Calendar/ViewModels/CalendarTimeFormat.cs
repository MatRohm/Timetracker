using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Calendar.Localization;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Duration formatting shared by the calendar: the short "h:mm" form the blocks,
/// day totals and month cells use, e.g. <c>1:30</c>.
/// </summary>
internal static class CalendarTimeFormat
{
    /// <summary>
    /// Seconds as "h:mm", e.g. 5400 → "1:30". Hours are not wrapped at 24, so a
    /// week total such as 25:10 stays readable.
    /// </summary>
    public static string HoursMinutes(double seconds)
    {
        var totalMinutes = (long)Math.Round(seconds / 60);
        var result = $"{totalMinutes / 60}:{totalMinutes % 60:00}";
        return result;
    }

    /// <summary>A duration as "h:mm", e.g. 1:30.</summary>
    public static string HoursMinutes(TimeSpan duration) => HoursMinutes(duration.TotalSeconds);

    /// <summary>A planned session change as "HH:mm–HH:mm → HH:mm–HH:mm" or "… removed".</summary>
    internal static string Describe(SessionChange change)
    {
        var original = ClockRange(change.Original.Start, change.Original.End);
        var result = change.Updated is { } updated
            ? $"{original} → {ClockRange(updated.Start, updated.End)}"
            : string.Format(Strings.Calendar_ChangeRemovedFormat, original);
        return result;
    }

    /// <summary>A clock range as "HH:mm–HH:mm", e.g. "10:15–11:05".</summary>
    public static string ClockRange(DateTimeOffset start, DateTimeOffset end) =>
        $"{start:HH\\:mm}–{end:HH\\:mm}";
}
