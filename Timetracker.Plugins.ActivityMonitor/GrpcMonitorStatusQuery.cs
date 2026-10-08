using Grpc.Core;
using Grpc.Net.Client;
using Timetracker.Plugins.ActivityMonitor.Grpc;
using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor;

/// <summary>
/// gRPC-backed <see cref="IActivityMonitorStatusQuery"/>: asks the monitor
/// process (which hosts the status service on loopback) whether it is alive.
/// A successful call means Running; a connection failure (no listener) means
/// Stopped; any other failure means Unknown.
/// </summary>
public sealed class GrpcMonitorStatusQuery : IActivityMonitorStatusQuery, IDisposable
{
    /// <summary>Loopback port the monitor's status service listens on.</summary>
    public const int Port = 50051;

    /// <summary>Loopback endpoint the monitor's status service listens on.</summary>
    public static string Address => $"http://127.0.0.1:{Port}";

    private readonly GrpcChannel _channel;
    private readonly MonitorStatusService.MonitorStatusServiceClient _client;

    public GrpcMonitorStatusQuery()
    {
        _channel = GrpcChannel.ForAddress(Address);
        _client = new MonitorStatusService.MonitorStatusServiceClient(_channel);
    }

    public async Task<MonitorStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            await _client.GetStatusAsync(
                new GetStatusRequest(),
                deadline: deadline,
                cancellationToken: cancellationToken);
            return MonitorStatus.Running;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            return MonitorStatus.Stopped;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return MonitorStatus.Unknown;
        }
    }

    public void Dispose() => _channel.Dispose();
}
