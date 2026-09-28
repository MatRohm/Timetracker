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
}
