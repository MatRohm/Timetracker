using System.Net;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins;

namespace Timetracker.AzureDevOps.Tests.Unit;

public sealed class AzureDevOpsTests
{
    [Test]
    public void AzureDevOpsConfig_WhenJsonFileExists_ShouldLoadItsValues()
    {
        var path = TempPath("config-ok.json");
        File.WriteAllText(path, """
            {
              "url": "https://dev.azure.com/my-org",
              "project": "MyProject",
              "pat": "secret-token"
            }
            """);

        var config = AzureDevOpsConfig.Load(path);

        config.Url.Should().Be("https://dev.azure.com/my-org");
        config.Project.Should().Be("MyProject");
        config.Pat.Should().Be("secret-token");
        config.IsUsable.Should().BeTrue();
    }

    [Test]
    public void AzureDevOpsConfig_WhenJsonFileIsMissing_ShouldYieldAnEmptyUnusableConfig()
    {
        var config = AzureDevOpsConfig.Load(TempPath("does-not-exist.json"));

        config.Url.Should().BeEmpty();
        config.IsUsable.Should().BeFalse();
    }

    [Test]
    public void AzureDevOpsConfig_WhenJsonFileIsBroken_ShouldYieldAnEmptyConfigInsteadOfCrashing()
    {
        var path = TempPath("config-broken.json");
        File.WriteAllText(path, "{ not valid json ");

        var config = AzureDevOpsConfig.Load(path);

        config.IsUsable.Should().BeFalse();
    }

    [Test]
    public void AzureDevOpsConfig_WhenPatIsMissing_ShouldNotBeUsable()
    {
        var config = new AzureDevOpsConfig
        {
            Url = "https://dev.azure.com/my-org",
            Project = "MyProject",
            Pat = "  ",
        };

        config.IsUsable.Should().BeFalse();
    }

    [Test]
    public async Task AzureDevOpsService_WhenWorkItemHasAzeElement_ShouldParseTitleAndAzeElement()
    {
        var http = FakeHttp.Create((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "id": 42,
                  "fields": {
                    "System.Title": "Fix login bug",
                    "Custom.2c1e4e3f-b6ad-4004-a072-a65e75547971": "10831 Genesis Abschnitte"
                  }
                }
                """, System.Text.Encoding.UTF8, "application/json"),
        });
        using var client = new AzureDevOpsClient(Config(), http);

        var workItem = await client.GetWorkItemAsync(42);

        workItem.Should().NotBeNull();
        workItem!.Id.Should().Be(42);
        workItem.Title.Should().Be("Fix login bug");
        workItem.AzeElement.Should().Be("10831 Genesis Abschnitte",
            "the AZE-Element field uses a GUID-based reference name in Azure DevOps");
    }

    [Test]
    public async Task AzureDevOpsService_WhenAzeElementUsesFriendlyFieldName_ShouldFallBackToIt()
    {
        var http = FakeHttp.Create((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "id": 42,
                  "fields": {
                    "System.Title": "Fix login bug",
                    "Custom.AZEElement": "Quarterly figures"
                  }
                }
                """, System.Text.Encoding.UTF8, "application/json"),
        });
        using var client = new AzureDevOpsClient(Config(), http);

        var workItem = await client.GetWorkItemAsync(42);

        workItem!.AzeElement.Should().Be("Quarterly figures");
    }

    [Test]
    public async Task AzureDevOpsService_WhenWorkItemIsMissing_ShouldReturnNull()
    {
        var http = FakeHttp.Create((_) => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new AzureDevOpsClient(Config(), http);

        var workItem = await client.GetWorkItemAsync(999);

        workItem.Should().BeNull();
    }

    [Test]
    public async Task AzureDevOpsService_WhenRequestIsSent_ShouldBuildUrlFromConfigAndAddBasicAuth()
    {
        HttpRequestMessage? seen = null;
        var http = FakeHttp.Create((request) =>
        {
            seen = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id": 1, "fields": {}}""",
                    System.Text.Encoding.UTF8, "application/json"),
            };
        });
        using var client = new AzureDevOpsClient(Config(), http);

        await client.GetWorkItemAsync(1);

        seen!.RequestUri!.ToString().Should().Be(
            "https://dev.azure.com/my-org/MyProject/_apis/wit/workitems/1?api-version=7.1");
        seen.Headers.Authorization!.Scheme.Should().Be("Basic");
        seen.Headers.Authorization!.Parameter.Should().NotBeEmpty();
    }

    [Test]
    public async Task AzureDevOpsService_WhenWorkItemExists_ShouldFillHostWithNumberTitleAndAzeElement()
    {
        using var service = new AzureDevOpsService(Config(), () => new AzureDevOpsClient(
            Config(), FakeHttp.WorkItem(42, "Fix login bug", "Quarterly figures")));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeTrue();
        host.TaskName.Should().Be("42 Fix login bug", "the name is '<issue number> <title>'");
        host.BookingElement.Should().Be("Quarterly figures");
        host.LastStatusKind.Should().Be(TrackerStatusKind.Success);
        host.LastStatusMessage.Should().Contain("42 Fix login bug");
    }

    [Test]
    public async Task AzureDevOpsService_WhenWorkItemIsMissing_ShouldReportAndKeepHostUntouched()
    {
        using var service = new AzureDevOpsService(Config(), () => new AzureDevOpsClient(
            Config(), FakeHttp.WorkItemNotFound()));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("999", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
        host.TaskName.Should().BeEmpty();
    }

    [Test]
    public async Task AzureDevOpsService_WhenIssueNumberIsNotNumeric_ShouldReportAnError()
    {
        using var service = new AzureDevOpsService(Config(), () => throw new InvalidOperationException());
        var host = new FakeHost();

        (await service.ApplyIssueAsync("abc", host)).Success.Should().BeFalse();
        (await service.ApplyIssueAsync("0", host)).Success.Should().BeFalse();
        (await service.ApplyIssueAsync("  ", host)).Success.Should().BeFalse();
    }

    [Test]
    public async Task AzureDevOpsService_WhenConfigIsMissing_ShouldReportWhereToPutIt()
    {
        using var service = new AzureDevOpsService(new AzureDevOpsConfig());
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timetracker-azdo.json");
    }

    [Test]
    public void AzureDevOpsService_WhenConfigurationHintIsRead_ShouldNameThePathAndShowAnExample()
    {
        using var service = new AzureDevOpsService(new AzureDevOpsConfig());

        var hint = service.ConfigurationHint;

        hint.Should().Contain(AzureDevOpsConfig.DefaultFilePath);
        hint.Should().Contain("\"url\"");
        hint.Should().Contain("\"project\"");
        hint.Should().Contain("\"pat\"");
        hint.Should().Contain("https://dev.azure.com/your-organization");
    }

    [Test]
    public async Task AzureDevOpsService_WhenHttpRequestFails_ShouldMapToErrorResult()
    {
        using var service = new AzureDevOpsService(Config(), () => new AzureDevOpsClient(
            Config(), FakeHttp.Create((_) => new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("failed");
    }

    private static AzureDevOpsConfig Config() => new()
    {
        Url = "https://dev.azure.com/my-org",
        Project = "MyProject",
        Pat = "secret-token",
    };

    private static string TempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "tt-azdo-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private static class FakeHttp
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

    private sealed class FakeHost : ITrackerUiHost
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
}
