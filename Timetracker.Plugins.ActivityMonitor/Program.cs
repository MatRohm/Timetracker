using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Timetracker.Plugins.ActivityMonitor.Services;
using Timetracker.Plugins.Contracts.Logging;

// Headless background monitor: hosts a gRPC status endpoint on loopback so the
// main app can ask whether the monitor is running, and runs the activity
// tracking loop (idle polling) as a hosted service. No window dependencies.

var builder = WebApplication.CreateBuilder(args);

// The status service is queried only by the local app, over HTTP/2 on loopback.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ListenLocalhost(GrpcMonitorStatusQuery.Port, listen => listen.Protocols = HttpProtocols.Http2));
builder.Services.AddGrpc();
builder.Services.AddHostedService<MonitorLoopService>();

// The monitor logs to its own file, not the console (it runs headless).
builder.Logging.ClearProviders();
builder.Logging.AddTimetrackerFile("monitor");

var app = builder.Build();
app.MapGrpcService<MonitorStatusServiceImpl>();

app.Run();
