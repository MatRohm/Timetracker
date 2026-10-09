using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

[TestFixture]
public sealed class AzureDevOpsServiceTests
{
    [Test]
    public async Task ApplyIssueAsync_WhenTheWorkItemHasABookingElement_ShouldParseTitleAndBookingElement()
    {
        var http = FakeHttp.Create((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "id": 42,
                  "fields": {
                    "System.Title": "Fix login bug",
                    "Custom.BookingElement": "10831 Genesis Abschnitte"
                  }
                }
                """, System.Text.Encoding.UTF8, "application/json"),
        });
        using var client = new AzureDevOpsClient(AzureDevOpsTestHelpers.Config(), http);

        var workItem = await client.GetWorkItemAsync(42);

        workItem.Should().NotBeNull();
        workItem!.Id.Should().Be(42);
        workItem.Title.Should().Be("Fix login bug");
        workItem.BookingElement.Should().Be("10831 Genesis Abschnitte",
            "the booking element is read from the configured custom field");
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheBookingElementFieldIsNotConfigured_ShouldLeaveTheBookingElementEmpty()
    {
        var config = AzureDevOpsTestHelpers.Config();
        config.BookingElementField = "";
        var http = FakeHttp.Create((request) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "id": 42,
                  "fields": {
                    "System.Title": "Fix login bug",
                    "Custom.BookingElement": "Quarterly figures"
                  }
                }
                """, System.Text.Encoding.UTF8, "application/json"),
        });
        using var client = new AzureDevOpsClient(config, http);

        var workItem = await client.GetWorkItemAsync(42);

        workItem!.BookingElement.Should().BeEmpty();
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
    public async Task ApplyIssueAsync_WhenTheWorkItemExists_ShouldFillTheHostWithNumberTitleAndBookingElement()
    {
        var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => new AzureDevOpsClient(
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
        var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => new AzureDevOpsClient(
            AzureDevOpsTestHelpers.Config(), FakeHttp.WorkItemNotFound()));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("999", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
        host.TaskName.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheRequestFails_ShouldLogTheError()
    {
        var logger = new FakeLogger<AzureDevOpsService>();
        var service = new AzureDevOpsService(
            AzureDevOpsTestHelpers.Config(),
            () => new AzureDevOpsClient(
                AzureDevOpsTestHelpers.Config(),
                FakeHttp.Create(_ => throw new HttpRequestException("no network"))),
            logger);

        var result = await service.ApplyIssueAsync("42", new FakeHost());

        result.Success.Should().BeFalse();
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
        logger.LatestRecord.Exception.Should().BeOfType<HttpRequestException>();
        logger.LatestRecord.Message.Should().Contain("42");
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheIssueNumberIsNotNumeric_ShouldReportAnError()
    {
        var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => throw new InvalidOperationException());
        var host = new FakeHost();

        (await service.ApplyIssueAsync("abc", host)).Success.Should().BeFalse();
        (await service.ApplyIssueAsync("0", host)).Success.Should().BeFalse();
        (await service.ApplyIssueAsync("  ", host)).Success.Should().BeFalse();
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheConfigIsMissing_ShouldPointToTheOptionsTab()
    {
        var service = new AzureDevOpsService(new AzureDevOpsConfig());
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(AzureDevOpsService.NotConfiguredMessage);
        result.Error.Should().Contain("Options tab");
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheConnectionWasEditedAfterStart_ShouldUseTheCurrentValues()
    {
        var current = new AzureDevOpsConfig();
        AzureDevOpsConfig? used = null;
        var service = new AzureDevOpsService(() => current, config =>
        {
            used = config;
            return new AzureDevOpsClient(config, FakeHttp.WorkItem(42, "Fix login bug", "10831"));
        });
        var host = new FakeHost();

        var before = await service.ApplyIssueAsync("42", host);
        current = AzureDevOpsTestHelpers.Config();
        var after = await service.ApplyIssueAsync("42", host);

        before.Success.Should().BeFalse("nothing was configured yet");
        after.Success.Should().BeTrue();
        used.Should().BeSameAs(current);
    }

    [Test]
    public async Task ApplyIssueAsync_WhenTheHttpRequestFails_ShouldMapToAnErrorResult()
    {
        var service = new AzureDevOpsService(AzureDevOpsTestHelpers.Config(), () => new AzureDevOpsClient(
            AzureDevOpsTestHelpers.Config(), FakeHttp.Create((_) => new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        var host = new FakeHost();

        var result = await service.ApplyIssueAsync("42", host);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("failed");
    }
}
