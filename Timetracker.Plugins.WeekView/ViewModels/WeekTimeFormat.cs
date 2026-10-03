using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Localization;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// Duration formatting shared by the week view: the short "h:mm" form the week
/// tree and the week total use, e.g. <c>1:30</c>.
/// </summary>
internal static class WeekTimeFormat
{
    /// <summary>Seconds as "h:mm", e.g. 5400 → "1:30".</summary>
    public static string HoursMinutes(double seconds) =>
        TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"h\:mm");

    /// <summary>A duration as "h:mm", e.g. 1:30.</summary>
    public static string HoursMinutes(TimeSpan duration) => HoursMinutes(duration.TotalSeconds);

    /// <summary>A planned session change as "HH:mm–HH:mm → HH:mm–HH:mm" or "… removed".</summary>
    internal static string Describe(SessionChange change)
    {
        var original = ClockRange(change.Original.Start, change.Original.End);
        var result = change.Updated is { } updated
            ? $"{original} → {ClockRange(updated.Start, updated.End)}"
            : string.Format(Strings.Week_ChangeRemovedFormat, original);
        return result;
    }

    /// <summary>A clock range as "HH:mm–HH:mm", e.g. "10:15–11:05".</summary>
    public static string ClockRange(DateTimeOffset start, DateTimeOffset end) =>
        $"{start:HH\\:mm}–{end:HH\\:mm}";
}
