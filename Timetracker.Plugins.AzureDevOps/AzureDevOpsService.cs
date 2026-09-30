using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// Fetches a work item by number and pushes its title and AZE-Element into the
/// tracker inputs: task name becomes "&lt;issue number&gt; &lt;title&gt;". Reads the
/// connection anew for every lookup, so edits in the options tab apply at once,
/// and owns the error reporting so the host stays thin.
/// </summary>
public sealed class AzureDevOpsService
{
    /// <summary>Shown when the import is used without a usable connection.</summary>
    public const string NotConfiguredMessage =
        "Azure DevOps is not configured. Enter the organization URL, project and "
        + "personal access token in the Azure DevOps section of the Options tab.";

    private readonly Func<AzureDevOpsConfig> _config;
    private readonly Func<AzureDevOpsConfig, AzureDevOpsClient> _clientFactory;
    private readonly ILogger<AzureDevOpsService> _logger;

    /// <param name="config">A fixed connection (used by tests).</param>
    /// <param name="clientFactory">Creates the HTTP client (replaced by tests).</param>
    /// <param name="logger">Receives failed lookups.</param>
    public AzureDevOpsService(
        AzureDevOpsConfig config,
        Func<AzureDevOpsClient>? clientFactory = null,
        ILogger<AzureDevOpsService>? logger = null)
        : this(() => config, clientFactory is null ? null : _ => clientFactory(), logger)
    {
    }

    /// <param name="currentConfig">Returns the connection to use now; read for every lookup.</param>
    /// <param name="clientFactory">Creates the HTTP client for a connection (replaced by tests).</param>
    /// <param name="logger">Receives failed lookups.</param>
    public AzureDevOpsService(
        Func<AzureDevOpsConfig> currentConfig,
        Func<AzureDevOpsConfig, AzureDevOpsClient>? clientFactory = null,
        ILogger<AzureDevOpsService>? logger = null)
    {
        _config = currentConfig;
        _clientFactory = clientFactory ?? (config => new AzureDevOpsClient(config));
        _logger = logger ?? NullLogger<AzureDevOpsService>.Instance;
    }

    /// <summary>True when the current connection has all three values.</summary>
    public bool IsConfigured => _config().IsUsable;

    /// <summary>
    /// Looks up the work item and fills the host inputs. Returns false when the
    /// issue number is invalid, the config is missing, or the request failed.
    /// The view model is not touched from here; the host decides what to do with
    /// the values (they are also returned for tests).
    /// </summary>
    public async Task<ApplyResult> ApplyIssueAsync(
        string issueNumberText, ITrackerUiCommand host, CancellationToken cancellationToken = default)
    {
        var numberText = issueNumberText.Trim();
        if (numberText.Length == 0 || !int.TryParse(numberText, out var id) || id <= 0)
        {
            return ApplyResult.Failure("Please enter a numeric issue number first.");
        }

        var config = _config();
        if (!config.IsUsable)
        {
            return ApplyResult.Failure(NotConfiguredMessage);
        }

        using var client = _clientFactory(config);
        WorkItemInfo? workItem;
        try
        {
            workItem = await client.GetWorkItemAsync(id, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or InvalidOperationException or System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, "Could not look up work item {Id}", id);
            return ApplyResult.Failure("Azure DevOps request failed: " + ex.Message);
        }

        if (workItem is null)
        {
            return ApplyResult.Failure($"Work item {id} was not found.");
        }

        var taskName = $"{workItem.Id} {workItem.Title}".Trim();
        host.SetTaskName(taskName);
        host.SetBookingElement(workItem.AzeElement);
        host.ShowStatus($"✓ Applied \"{taskName}\""
            + (workItem.AzeElement.Length > 0 ? $" ({workItem.AzeElement})" : ""),
            TrackerStatusKind.Success);

        var result = ApplyResult.Successful(taskName, workItem.AzeElement);
        return result;
    }

    /// <summary>Outcome of an apply attempt; used for status display and tests.</summary>
    public sealed record ApplyResult(bool Success, string TaskName, string BookingElement, string? Error)
    {
        public static ApplyResult Successful(string taskName, string bookingElement) =>
            new(true, taskName, bookingElement, null);

        public static ApplyResult Failure(string message) => new(false, "", "", message);
    }
}
