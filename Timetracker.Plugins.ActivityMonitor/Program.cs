using Microsoft.Extensions.Logging;
using Timetracker.Plugins.ActivityMonitor;
using Timetracker.Plugins.Contracts.Logging;

// Headless background monitor: starts the tracker, polls the idle state on a
// timer and closes the open span when the process or session ends. No UI and no
// window dependencies, so it runs the same on Windows and Linux.

// The monitor's own log file (monitor-<date>.log), next to the app's; flushed on exit.
using var loggerFactory = LoggerFactory.Create(builder => builder.AddTimetrackerFile("monitor"));
var log = new ActivityLog();

// The monitor has no DI container; it reads the idle-span threshold from the
// options file the app writes, falling back to one hour.
var idleSpanThreshold = IdleOptions.IdleSpanThreshold(
    IdleOptions.ReadValueFromOptionsFile(IdleOptions.OptionsFilePath, IdleOptions.IdleSpanThresholdKey));
var tracker = new ActivityTracker(log, logger: loggerFactory.CreateLogger<ActivityTracker>(), idleSpanThreshold: idleSpanThreshold);
tracker.Start();

using var stopping = new CancellationTokenSource();

// Ctrl+C: close the open span so the log stays consistent.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopping.Cancel();
};

// SIGTERM (logoff/shutdown) ends the loop gracefully; ProcessExit closes the span.
IDisposable? terminationSignal = null;
if (!OperatingSystem.IsWindows())
{
    terminationSignal = SessionSignals.RegisterTermination(stopping);
}
AppDomain.CurrentDomain.ProcessExit += (_, _) => tracker.Stop();

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
try
{
    while (await timer.WaitForNextTickAsync(stopping.Token))
    {
        tracker.Poll();
    }
}
catch (OperationCanceledException)
{
    // Normal shutdown: the spans are closed below.
}

terminationSignal?.Dispose();
tracker.Stop();
