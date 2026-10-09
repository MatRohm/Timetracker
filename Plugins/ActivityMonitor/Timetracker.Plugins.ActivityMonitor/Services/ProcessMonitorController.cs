using System.ComponentModel;
using System.Diagnostics;
using Grpc.Net.Client;
using Timetracker.Plugins.ActivityMonitor.Grpc;
using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// Launches and gracefully stops the monitor process. Start launches the
/// installed executable detached; Stop sends the loopback gRPC Stop RPC so the
/// monitor shuts down gracefully (closing the open activity span) rather than
/// being killed.
/// </summary>
public sealed class ProcessMonitorController : IActivityMonitorController, IDisposable
{
    private readonly IActivityMonitorInstaller _installer;
    private readonly GrpcChannel _channel;
    private readonly MonitorStatusService.MonitorStatusServiceClient _client;

    public ProcessMonitorController(IActivityMonitorInstaller installer)
        : this(installer, GrpcChannel.ForAddress(MonitorEndpoint.Address))
    {
    }

    /// <summary>Test seam: inject a channel (e.g. backed by a fake HTTP handler).</summary>
    internal ProcessMonitorController(IActivityMonitorInstaller installer, GrpcChannel channel)
    {
        _installer = installer;
        _channel = channel;
        _client = new MonitorStatusService.MonitorStatusServiceClient(channel);
    }

    public bool Start()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_installer.MonitorExePath) { CreateNoWindow = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            await _client.StopAsync(new StopRequest(), deadline: deadline, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose() => _channel.Dispose();
}
