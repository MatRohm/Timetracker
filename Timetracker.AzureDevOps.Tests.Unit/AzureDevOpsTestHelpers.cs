using System.Net;
using Timetracker.Plugins;

namespace Timetracker.AzureDevOps.Tests.Unit;

/// <summary>Shared data and test doubles for the Azure DevOps unit tests.</summary>
internal static class AzureDevOpsTestHelpers
{
    internal static AzureDevOpsConfig Config() => new()
    {
        Url = "https://dev.azure.com/my-org",
        Project = "MyProject",
        Pat = "secret-token",
    };

    internal static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "tt-azdo-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}

internal static class FakeHttp
{
    public static HttpClient Create(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        return new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com") };
    }

    public static HttpClient WorkItem(int id, string title, string azeElement) =>
        Create((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id": {{id}}, "fields": {"System.Title": "{{title}}", "Custom.AZEElement": "{{azeElement}}"} }""",
                System.Text.Encoding.UTF8, "application/json"),
        });

    public static HttpClient WorkItemNotFound() =>
        Create((_) => new HttpResponseMessage(HttpStatusCode.NotFound));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responder(request);
            return Task.FromResult(response);
        }
    }
}

internal sealed class FakeHost : ITrackerUiHost
{
    public string TaskName { get; private set; } = "";

    public string BookingElement { get; private set; } = "";

    public string? LastStatusMessage { get; private set; }

    public TrackerStatusKind? LastStatusKind { get; private set; }

    public void SetTaskName(string taskName) => TaskName = taskName;

    public void SetBookingElement(string bookingElement) => BookingElement = bookingElement;

    public void ShowStatus(string message, TrackerStatusKind kind)
    {
        LastStatusMessage = message;
        LastStatusKind = kind;
    }
}
