namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// The loopback endpoint the monitor's status service listens on, shared by the
/// server (Program.cs) and the app's client (<see cref="GrpcMonitorStatusQuery"/>)
/// so the two processes agree on where to connect.
/// </summary>
public static class MonitorEndpoint
{
    /// <summary>Loopback port the monitor's status service listens on.</summary>
    public const int Port = 50051;

    /// <summary>Loopback address the app's client dials.</summary>
    public static string Address => $"http://127.0.0.1:{Port}";
}
