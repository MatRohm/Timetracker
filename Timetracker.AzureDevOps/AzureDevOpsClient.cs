using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Timetracker.AzureDevOps;

/// <summary>Result of a work-item lookup.</summary>
public sealed record WorkItemInfo(int Id, string Title, string AzeElement)
{
    public bool IsEmpty => Id == 0;
}

/// <summary>
/// Reads work items from the Azure DevOps REST API. Authentication uses the PAT
/// as the Basic password with an empty user name, as documented by Microsoft.
/// </summary>
public sealed class AzureDevOpsClient : IDisposable
{
    /// <summary>
    /// Reference name of the custom field holding the booking element ("AZE-Element").
    /// Fields created in Azure DevOps get a GUID-based reference name; the friendly
    /// variant is kept as a fallback for organizations where it exists.
    /// </summary>
    public const string AzeElementField = "Custom.2c1e4e3f-b6ad-4004-a072-a65e75547971";

    /// <summary>Fallback reference name of the AZE-Element field.</summary>
    public const string AzeElementFallbackField = "Custom.AZEElement";

    private const string ApiVersion = "api-version=7.1";

    private readonly HttpClient _http;
    private readonly Uri _baseUrl;
    private readonly string _basicToken;

    /// <param name="http">Optional HttpClient override (used by tests); null creates one.</param>
    public AzureDevOpsClient(AzureDevOpsConfig config, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        if (http is null)
        {
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        // PAT as Basic password with an empty user name.
        _basicToken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($":{config.Pat.Trim()}"));
        _baseUrl = BuildBaseUrl(config);
    }

    private static Uri BuildBaseUrl(AzureDevOpsConfig config)
    {
        var url = config.Url.Trim().TrimEnd('/');
        return new Uri($"{url}/{Uri.EscapeDataString(config.Project.Trim())}/_apis/wit/workitems/");
    }

    /// <summary>
    /// Fetches the work item and maps it to <see cref="WorkItemInfo"/>: title plus
    /// the custom "AZE-Element" field. Returns null when the item does not exist.
    /// </summary>
    public async Task<WorkItemInfo?> GetWorkItemAsync(
        int id, CancellationToken cancellationToken = default)
    {
        var url = new Uri(_baseUrl, $"{id}?{ApiVersion}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", _basicToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<WorkItemPayload>(JsonOptions, cancellationToken);
        if (payload is null)
        {
            return null;
        }

        var title = ReadField(payload.Fields, "System.Title");
        var azeElement = ReadField(payload.Fields, AzeElementField);
        if (azeElement.Length == 0)
        {
            azeElement = ReadField(payload.Fields, AzeElementFallbackField);
        }

        var result = new WorkItemInfo(payload.Id, title, azeElement);
        return result;
    }

    private static string ReadField(Dictionary<string, JsonElement>? fields, string name)
    {
        if (fields is null || !fields.TryGetValue(name, out var value))
        {
            return "";
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            _ => value.ToString(),
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class WorkItemPayload
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, JsonElement>? Fields { get; set; }
    }

    public void Dispose() => _http.Dispose();
}
