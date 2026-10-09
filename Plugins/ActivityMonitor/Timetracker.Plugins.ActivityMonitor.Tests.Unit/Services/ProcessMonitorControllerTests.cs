using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Grpc.Net.Client;
using NUnit.Framework;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Services;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit.Services;

[TestFixture]
public sealed class ProcessMonitorControllerTests
{
    [Test]
    public void Start_WhenExeMissing_ShouldReturnFalse()
    {
        using var controller = new ProcessMonitorController(new MissingExeInstaller());

        var result = controller.Start();

        result.Should().BeFalse("a missing executable cannot be launched");
    }

    [Test]
    public async Task StopAsync_WhenServerAnswers_ShouldReturnTrue()
    {
        using var controller = new ProcessMonitorController(
            new MissingExeInstaller(), Channel(new RespondingHandler()));

        var result = await controller.StopAsync();

        result.Should().BeTrue("a successful Stop RPC means the monitor stopped gracefully");
    }

    [Test]
    public async Task StopAsync_WhenServerFails_ShouldReturnFalse()
    {
        using var controller = new ProcessMonitorController(
            new MissingExeInstaller(), Channel(new FailingHandler()));

        var result = await controller.StopAsync();

        result.Should().BeFalse("a transport failure means the monitor could not be stopped");
    }

    private static GrpcChannel Channel(HttpMessageHandler handler) =>
        GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });

    /// <summary>An installer whose executable path never exists, so Start never launches.</summary>
    private sealed class MissingExeInstaller : IActivityMonitorInstaller
    {
        public string MonitorExePath => "/nonexistent/timetracker-monitor";

        public bool IsInstalled => true;

        public bool Install() => true;

        public bool Uninstall() => true;
    }

    /// <summary>Answers every gRPC call with an empty, successful Stop response.</summary>
    private sealed class RespondingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Version = HttpVersion.Version20;
            // One gRPC frame: uncompressed (0x00) with a zero-length message (an empty StopResponse).
            response.Content = new ByteArrayContent(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 });
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            response.TrailingHeaders.Add("grpc-status", "0");
            return Task.FromResult(response);
        }
    }

    /// <summary>Fails every gRPC call with a transport error.</summary>
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
