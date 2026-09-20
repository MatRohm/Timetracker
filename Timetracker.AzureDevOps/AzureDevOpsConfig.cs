using System.Text.Json;

namespace Timetracker.AzureDevOps;

/// <summary>
/// One Azure DevOps connection: organization/collection URL, project name and a
/// personal access token. Persisted at <c>%USERPROFILE%\timetracker-azdo.json</c>;
/// the file is provided and maintained by the end user.
/// </summary>
public sealed class AzureDevOpsConfig
{
    /// <summary>Collection/organization URL, e.g. "https://dev.azure.com/your-org".</summary>
    public string Url { get; set; } = "";

    /// <summary>Name of the project the work items live in.</summary>
    public string Project { get; set; } = "";

    /// <summary>Personal access token used as Basic password (user name is empty).</summary>
    public string Pat { get; set; } = "";

    /// <summary>True when the values look complete enough for a request.</summary>
    public bool IsUsable =>
        Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var url)
        && url.Scheme is "http" or "https"
        && Project.Trim().Length > 0
        && Pat.Trim().Length > 0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Default path of the configuration file.</summary>
    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "timetracker-azdo.json");

    /// <summary>Loads the config; missing or broken files yield an empty config.</summary>
    public static AzureDevOpsConfig Load(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        try
        {
            if (!File.Exists(path))
            {
                return new AzureDevOpsConfig();
            }
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new AzureDevOpsConfig();
            }
            var config = JsonSerializer.Deserialize<AzureDevOpsConfig>(text, JsonOptions);
            return config ?? new AzureDevOpsConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable config must not break the app; treat it as unconfigured.
            ErrorLogAdapter.Log("AzureDevOps config load", ex);
            return new AzureDevOpsConfig();
        }
    }
}
