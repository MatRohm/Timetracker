using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// The "Azure DevOps" section of the options tab: organization URL, project and the
/// personal access token (masked).
/// </summary>
public sealed class AzureDevOpsOptionsContributor : IOptionDefinitionQuery
{
    public string Section => "Azure DevOps";

    public IReadOnlyList<OptionDefinition> Options { get; } =
    [
        new(
            AzureDevOpsSettings.UrlKey,
            "Organization URL",
            OptionKind.Text,
            HintText: "e.g. https://dev.azure.com/your-org"),
        new(AzureDevOpsSettings.ProjectKey, "Project", OptionKind.Text),
        new(
            AzureDevOpsSettings.PatKey,
            "Personal access token",
            OptionKind.Secret,
            HintText: "Azure DevOps personal access token; used as Basic password, user name stays empty"),
        new(
            AzureDevOpsSettings.BookingElementFieldKey,
            "Booking element field",
            OptionKind.Text,
            HintText: "Reference name of the custom field holding the booking element"),
    ];
}
