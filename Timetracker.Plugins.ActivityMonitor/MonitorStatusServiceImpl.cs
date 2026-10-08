using Grpc.Core;
using Timetracker.Plugins.ActivityMonitor.Grpc;

namespace Timetracker.Plugins.ActivityMonitor;

/// <summary>
/// gRPC status service hosted by the monitor process: answers "alive" so the
/// main app can tell a running monitor from a stopped one. The reply is empty;
/// a successful call is the whole signal.
/// </summary>
public sealed class MonitorStatusServiceImpl : MonitorStatusService.MonitorStatusServiceBase
{
    public override Task<GetStatusResponse> GetStatus(
        GetStatusRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetStatusResponse());
}
