namespace Timetracker.Services;

/// <summary>
/// Append-only error log next to the executable (execution directory). Every entry
/// is timestamped; the file is never deleted or rotated. If the execution directory
/// is not writable (e.g. the exe lives in Program Files), it falls back to %TEMP%.
/// </summary>
public static class ErrorLog
{
    private static readonly object Gate = new();
    private static string? _resolvedPath;

    /// <summary>Full path of the log file; resolved once on first use.</summary>
    public static string FilePath => _resolvedPath ??= ResolvePath();

    public static void Log(string context, Exception exception) =>
        Write(Format(context, exception.ToString()));

    public static void Log(string context, string message) =>
        Write(Format(context, message));

    private static string ResolvePath()
    {
        // First choice: the directory the application is executed from.
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Timetracker.log");
            File.AppendAllText(path, string.Empty);
            return path;
        }
        catch (Exception)
        {
            // No write access there - keep logs in the temp folder instead.
            return Path.Combine(Path.GetTempPath(), "Timetracker.log");
        }
    }

    private static string Format(string context, string text) =>
        $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{context}]{Environment.NewLine}{text}";

    private static void Write(string entry)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(FilePath, entry + Environment.NewLine +
                    new string('-', 60) + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never throw - it would mask the original error.
        }
    }
}
