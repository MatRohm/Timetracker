using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// The Azure DevOps connection as edited in the options tab; the values are kept
/// in the <see cref="IOptionQuery"/>.
/// </summary>
public sealed class AzureDevOpsSettings(IOptionQuery store)
{
    public const string UrlKey = "AzureDevOps.Url";
    public const string ProjectKey = "AzureDevOps.Project";
    public const string PatKey = "AzureDevOps.Pat";

    /// <summary>The connection to use now; an option that was never saved is empty.</summary>
    public AzureDevOpsConfig Current()
    {
        var result = new AzureDevOpsConfig
        {
            Url = store.GetValue(UrlKey) ?? "",
            Project = store.GetValue(ProjectKey) ?? "",
            Pat = store.GetValue(PatKey) ?? "",
        };
        return result;
    }
}
