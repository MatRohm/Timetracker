using System.Runtime.InteropServices;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Registers the process signals that mean "the session is ending" (logoff,
/// shutdown, service stop) so the open activity span is closed before exit.
/// SIGINT is already covered by the console cancel handler in Program.
/// </summary>
internal static class SessionSignals
{
    /// <summary>Hooks SIGTERM where the runtime supports it. Returns a disposable registration.</summary>
    public static IDisposable? RegisterTermination(
        CancellationTokenSource stopping)
    {
        try
        {
            var registration = PosixSignalRegistration.Create(
                PosixSignal.SIGTERM,
                context =>
                {
                    // Let the polling loop exit gracefully; Stop happens after it.
                    context.Cancel = true;
                    stopping.Cancel();
                });
            return registration;
        }
        catch (PlatformNotSupportedException)
        {
            // Not available on this platform; ProcessExit still closes the span.
            return null;
        }
    }
}
