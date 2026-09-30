using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// Fetches a work item by number and pushes its title and AZE-Element into the
/// tracker inputs: task name becomes "&lt;issue number&gt; &lt;title&gt;". Owns the
/// config load and error reporting so the host stays thin.
/// </summary>
public sealed class AzureDevOpsService
{
    private readonly AzureDevOpsConfig _config;
    private readonly Func<AzureDevOpsClient> _clientFactory;
    private readonly ILogger<AzureDevOpsService> _logger;

    /// <param name="configFilePath">Overrides the config path (used by tests).</param>
    public AzureDevOpsService(string? configFilePath = null)
        : this(AzureDevOpsConfig.Load(configFilePath))
    {
    }

    /// <param name="config">The connection to use.</param>
    /// <param name="clientFactory">Creates the HTTP client (replaced by tests).</param>
    /// <param name="logger">Receives failed lookups.</param>
    public AzureDevOpsService(
        AzureDevOpsConfig config,
        Func<AzureDevOpsClient>? clientFactory = null,
        ILogger<AzureDevOpsService>? logger = null)
    {
        _config = config;
        _clientFactory = clientFactory ?? (() => new AzureDevOpsClient(config));
        _logger = logger ?? NullLogger<AzureDevOpsService>.Instance;
    }

    /// <summary>True when a config file with all three values was found.</summary>
    public bool IsConfigured => _config.IsUsable;

    /// <summary>Path of the config file the user is asked to create.</summary>
    public string ConfigFilePath => AzureDevOpsConfig.DefaultFilePath;

    /// <summary>
    /// Message shown when the import is used without a usable config: the expected
    /// file path plus a sample configuration to copy.
    /// </summary>
    public string ConfigurationHint => "Azure DevOps is not configured.\n\n" +
        "Create a configuration file at\n" + AzureDevOpsConfig.DefaultFilePath + "\n\n" +
        "Example content:\n" +
        "{\n" +
        "  \"url\": \"https://dev.azure.com/your-organization\",\n" +
        "  \"project\": \"YourProject\",\n" +
        "  \"pat\": \"your-personal-access-token\"\n" +
        "}";

    /// <summary>
    /// Looks up the work item and fills the host inputs. Returns false when the
    /// issue number is invalid, the config is missing, or the request failed.
    /// The view model is not touched from here; the host decides what to do with
    /// the values (they are also returned for tests).
    /// </summary>
    public async Task<ApplyResult> ApplyIssueAsync(
        string issueNumberText, ITrackerUiHost host, CancellationToken cancellationToken = default)
    {
        var numberText = issueNumberText.Trim();
        if (numberText.Length == 0 || !int.TryParse(numberText, out var id) || id <= 0)
        {
            return ApplyResult.Failure("Please enter a numeric issue number first.");
        }

        if (!_config.IsUsable)
        {
            return ApplyResult.Failure(
                "Azure DevOps is not configured. Create " + AzureDevOpsConfig.DefaultFilePath
                + " with \"url\", \"project\" and \"pat\".");
        }

        using var client = _clientFactory();
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
