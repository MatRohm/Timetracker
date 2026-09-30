namespace Timetracker.App.ViewModels;

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

    /// <summary>A clock range as "HH:mm–HH:mm", e.g. "10:15–11:05".</summary>
    public static string ClockRange(DateTimeOffset start, DateTimeOffset end) =>
        $"{start:HH\\:mm}–{end:HH\\:mm}";
}
