namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Shared helpers for the activity monitor unit tests.</summary>
internal static class TestSupport
{
    internal static DateTimeOffset At(int hour, int minute) =>
        new(2026, 9, 21, hour, minute, 0, TimeSpan.FromHours(2));

    internal static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "tt-activity-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
