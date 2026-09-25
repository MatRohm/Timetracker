using System.Runtime.CompilerServices;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Append-only text log of the activity monitor process, written next to its
/// executable. Every entry is timestamped and tagged with the calling method, so a
/// run can be followed end to end (startup, polling, state handling, shutdown).
/// Logging never throws: a monitor that cannot write its log must still record
/// activity.
/// </summary>
public sealed class MonitorLog
{
    private readonly object _gate = new();
    private readonly string _path;

    /// <param name="filePath">Overrides the default path (used by tests).</param>
    public MonitorLog(string? filePath = null)
    {
        _path = filePath ?? DefaultFilePath;
    }

    /// <summary>Log file next to the executable.</summary>
    public static string DefaultFilePath => Path.Combine(
        AppContext.BaseDirectory, "Timetracker.ActivityMonitor.log");

    public string FilePath => _path;

    /// <summary>Writes an informational entry tagged with the calling method.</summary>
    public void Info(string message, [CallerMemberName] string method = "") =>
        Write(method, message);

    /// <summary>Writes an error entry tagged with the calling method.</summary>
    public void Error(string message, Exception exception, [CallerMemberName] string method = "") =>
        Write(method, message + Environment.NewLine + exception);

    private void Write(string method, string message)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(
                    _path,
                    $"=== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{method}]{Environment.NewLine}" +
                    message + Environment.NewLine +
                    new string('-', 60) + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never take the monitor down.
        }
    }
}
