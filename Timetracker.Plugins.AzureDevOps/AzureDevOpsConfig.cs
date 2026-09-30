namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// One Azure DevOps connection: organization/collection URL, project name and a
/// personal access token. The connection is edited in the options tab (see
/// <see cref="AzureDevOpsSettings"/>).
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
}
