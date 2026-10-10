using System.Net;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

internal static class FakeHttp
{
    public static HttpClient Create(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        return new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com") };
    }

    public static HttpClient WorkItem(int id, string title, string bookingElement) =>
        Create((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id": {{id}}, "fields": {"System.Title": "{{title}}", "Custom.BookingElement": "{{bookingElement}}"} }""",
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
