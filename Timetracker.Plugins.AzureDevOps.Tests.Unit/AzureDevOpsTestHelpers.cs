using System.Net;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

/// <summary>Shared data and test doubles for the Azure DevOps unit tests.</summary>
internal static class AzureDevOpsTestHelpers
{
    internal static AzureDevOpsConfig Config() => new()
    {
        Url = "https://dev.azure.com/my-org",
        Project = "MyProject",
        Pat = "secret-token",
    };
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

/// <summary>Options kept in memory; tests set <see cref="Values"/> directly.</summary>
internal sealed class InMemoryOptionsStore : IOptionQuery, IOptionCommand
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetValue(string key) => Values.GetValueOrDefault(key);

    public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            Values.Remove(key);
        }
        else
        {
            Values[key] = value;
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeHost : ITrackerUiCommand
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
