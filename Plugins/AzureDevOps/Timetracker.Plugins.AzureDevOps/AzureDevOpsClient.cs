using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// Reads work items from the Azure DevOps REST API. Authentication uses the PAT
/// as the Basic password with an empty user name, as documented by Microsoft.
/// </summary>
public sealed class AzureDevOpsClient : IDisposable
{
    /// <summary>Per-request timeout applied to the client's own HttpClient.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private const string ApiVersion = "api-version=7.1";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _baseUrl;
    private readonly string _basicToken;
    private readonly string _bookingElementField;

    /// <param name="http">Optional HttpClient override (used by tests); null creates one.</param>
    public AzureDevOpsClient(AzureDevOpsConfig config, HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        if (_ownsHttp)
        {
            _http.Timeout = RequestTimeout;
        }

        // PAT as Basic password with an empty user name.
        _basicToken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($":{config.Pat.Trim()}"));
        _baseUrl = BuildBaseUrl(config);
        _bookingElementField = config.BookingElementField.Trim();
    }

    private static Uri BuildBaseUrl(AzureDevOpsConfig config)
    {
        var url = config.Url.Trim().TrimEnd('/');
        return new Uri($"{url}/{Uri.EscapeDataString(config.Project.Trim())}/_apis/wit/workitems/");
    }

    /// <summary>
    /// Fetches the work item and maps it to <see cref="WorkItemInfo"/>: title plus
    /// the configured booking-element field. Returns null when the item does not
    /// exist.
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
        var bookingElement = _bookingElementField.Length == 0
            ? ""
            : ReadField(payload.Fields, _bookingElementField);

        var result = new WorkItemInfo(payload.Id, title, bookingElement);
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

    public void Dispose()
    {
        // Only the client that created its own transport disposes it; a shared
        // HttpClient (injected by the service) outlives any single client.
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
