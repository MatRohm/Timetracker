using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// Runs the activity tracking loop inside the monitor process: starts the
/// tracker, polls the idle state on a timer, and closes the open span when the
/// host shuts down. Moved out of Program.cs so the gRPC host and the tracker
/// coexist under one <see cref="IHost"/>.
/// </summary>
public sealed class MonitorLoopService(ILogger<ActivityTracker> logger) : BackgroundService
{
    private readonly ILogger<ActivityTracker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var folder = TimetrackerPaths.ResolveFolder(
            IdleOptions.ReadValueFromOptionsFile(IdleOptions.OptionsFilePath, TimetrackerPaths.ConfigFolderKey));
        var log = new ActivityLog(Path.Combine(folder, "timetracker-activity.json"));
        var idleSpanThreshold = IdleOptions.IdleSpanThreshold(
            IdleOptions.ReadValueFromOptionsFile(IdleOptions.OptionsFilePath, IdleOptions.IdleSpanThresholdKey));
        var tracker = new ActivityTracker(log, logger: _logger, idleSpanThreshold: idleSpanThreshold);
        tracker.UseStateFile(Path.Combine(folder, "timetracker-activity.state.json"));
        tracker.Start();

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                tracker.Poll();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: the open span is closed below.
        }

        tracker.Stop();
    }
}
