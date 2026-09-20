namespace Timetracker.AzureDevOps;

/// <summary>
/// Append-only error log next to the executable, mirroring the main app's
/// ErrorLog behavior so the add-in does not need a project reference to it.
/// Falls back to %TEMP% when the execution directory is not writable.
/// </summary>
internal static class ErrorLogAdapter
{
    private static readonly object Gate = new();
    private static string? _resolvedPath;

    private static string ResolvePath()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Timetracker.log");
            File.AppendAllText(path, string.Empty);
            return path;
        }
        catch (Exception)
        {
            return Path.Combine(Path.GetTempPath(), "Timetracker.log");
        }
    }

    public static void Log(string context, Exception exception) =>
        Write($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{context}]{Environment.NewLine}{exception}");

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
            // Logging must never throw.
        }
    }

    private static string FilePath => _resolvedPath ??= ResolvePath();
}
