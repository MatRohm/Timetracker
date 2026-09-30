using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Timetracker.Plugins.Contracts.Logging;

/// <summary>
/// The file logging shared by every Timetracker process: Serilog writes one file
/// per process and day to <see cref="LogLocation.Directory"/>, e.g.
/// <c>app-20260930.log</c>, starts a new file past <see cref="FileSizeLimitBytes"/>
/// and keeps the newest <see cref="RetainedFileCount"/> files. Components log through
/// <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}"/> only.
/// </summary>
public static class FileLogging
{
    /// <summary>Size at which a new file is started for the rest of the day.</summary>
    public const long FileSizeLimitBytes = 10 * 1024 * 1024;

    /// <summary>Number of log files kept per process; older ones are deleted.</summary>
    public const int RetainedFileCount = 14;

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Adds the Timetracker log file of the process <paramref name="processName"/>
    /// (e.g. "app", "monitor"). The file is only opened once the logger factory is
    /// created, and is flushed and closed when the factory is disposed.
    /// </summary>
    /// <param name="directory">Overrides <see cref="LogLocation.Directory"/> (used by tests).</param>
    public static ILoggingBuilder AddTimetrackerFile(
        this ILoggingBuilder builder, string processName, string? directory = null)
    {
        var path = Path.Combine(directory ?? LogLocation.Directory, processName + "-.log");
        builder.Services.AddSingleton<ILoggerProvider>(_ =>
            new SerilogLoggerProvider(CreateFileLogger(path), dispose: true));
        return builder;
    }

    private static Serilog.Core.Logger CreateFileLogger(string path)
    {
        // Shared: the app and a second process (or instance) may append at once.
        var result = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                path,
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: RetainedFileCount,
                shared: true)
            .CreateLogger();
        return result;
    }
}
