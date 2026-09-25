using System.Net;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins;

namespace Timetracker.AzureDevOps.Tests.Unit;

[TestFixture]
public sealed class AzureDevOpsServiceTests
{
    [Test]
    public async Task ApplyIssueAsync_WhenTheWorkItemHasAnAzeElement_ShouldParseTitleAndAzeElement()
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
        using var client = new AzureDevOpsClient(AzureDevOpsTestHelpers.Config(), http);

        var workItem = await client.GetWorkItemAsync(42);

        workItem.Should().NotBeNull();
        workItem!.Id.Should().Be(42);
        workItem.Title.Should().Be("Fix login bug");
        workItem.AzeElement.Should().Be("10831 Genesis Abschnitte",
            "the AZE-Element field uses a GUID-based reference name in Azure DevOps");
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheAzeElementUsesTheFriendlyFieldName_ShouldFallBackToIt()
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
        using var client = new AzureDevOpsClient(AzureDevOpsTestHelpers.Config(), http);

        var workItem = await client.GetWorkItemAsync(42);

        workItem!.AzeElement.Should().Be("Quarterly figures");
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheWorkItemIsMissing_ShouldReportAnError()
    {
        var http = FakeHttp.Create((_) => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new AzureDevOpsClient(AzureDevOpsTestHelpers.Config(), http);

        var workItem = await client.GetWorkItemAsync(999);

        workItem.Should().BeNull();
    }

    [Test]
    public async Task ApplyIssueAsync_WhenARequestIsSent_ShouldBuildTheUrlFromConfigAndAddBasicAuth()
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
        using var client = new AzureDevOpsClient(AzureDevOpsTestHelpers.Config(), http);

        await client.GetWorkItemAsync(1);

        seen!.RequestUri!.ToString().Should().Be(
            "https://dev.azure.com/my-org/MyProject/_apis/wit/workitems/1?api-version=7.1");
        seen.Headers.Authorization!.Scheme.Should().Be("Basic");
        seen.Headers.Authorization!.Parameter.Should().NotBeEmpty();
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheWorkItemExists_ShouldFillTheHostWithNumberTitleAndAzeElement()
    {
        using var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => new AzureDevOpsClient(
            AzureDevOpsTestHelpers.Config(), FakeHttp.WorkItem(42, "Fix login bug", "Quarterly figures")));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeTrue();
        host.TaskName.Should().Be("42 Fix login bug", "the name is '<issue number> <title>'");
        host.BookingElement.Should().Be("Quarterly figures");
        host.LastStatusKind.Should().Be(TrackerStatusKind.Success);
        host.LastStatusMessage.Should().Contain("42 Fix login bug");
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheWorkItemIsMissing_ShouldKeepTheHostUntouched()
    {
        using var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => new AzureDevOpsClient(
            AzureDevOpsTestHelpers.Config(), FakeHttp.WorkItemNotFound()));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("999", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
        host.TaskName.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheIssueNumberIsNotNumeric_ShouldReportAnError()
    {
        using var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => throw new InvalidOperationException());
        var host = new FakeHost();

        (await service.ApplyIssueAsync("abc", host)).Success.Should().BeFalse();
        (await service.ApplyIssueAsync("0", host)).Success.Should().BeFalse();
        (await service.ApplyIssueAsync("  ", host)).Success.Should().BeFalse();
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheConfigIsMissing_ShouldReportWhereToPutIt()
    {
        using var service = new AzureDevOpsService(new AzureDevOpsConfig());
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timetracker-azdo.json");
    }

    [Test]
    public void ConfigurationHint_WhenRead_ShouldNameThePathAndShowAnExample()
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
    public async Task ApplyIssueAsync_WhenTheHttpRequestFails_ShouldMapToAnErrorResult()
    {
        using var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => new AzureDevOpsClient(
            AzureDevOpsTestHelpers.Config(), FakeHttp.Create((_) => new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("failed");
    }
}
