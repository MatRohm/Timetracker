using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Timetracker.Plugins.ActivityMonitor.Grpc;

namespace Timetracker.Plugins.ActivityMonitor.App.Services;

/// <summary>
/// gRPC status service hosted by the monitor process: answers "alive" so the
/// main app can tell a running monitor from a stopped one, and stops the process
/// gracefully on request. The reply is empty; a successful call is the whole
/// signal.
/// </summary>
public sealed class MonitorStatusServiceImpl(IHostApplicationLifetime lifetime) : MonitorStatusService.MonitorStatusServiceBase
{
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    public override Task<GetStatusResponse> GetStatus(
        GetStatusRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetStatusResponse());

    public override Task<StopResponse> Stop(
        StopRequest request,
        ServerCallContext context)
    {
        // Graceful shutdown: the host stops MonitorLoopService (which closes the
        // open activity span) and then the process exits. Deferred briefly so this
        // reply reaches the caller before the server stops.
        _ = Task.Delay(200).ContinueWith(_ => _lifetime.StopApplication());
        return Task.FromResult(new StopResponse());
    }
}
