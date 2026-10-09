using Timetracker.Plugins.AzureDevOps.Localization;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.AzureDevOps;

/// <summary>
/// The "Azure DevOps" section of the options tab: organization URL, project and the
/// personal access token (masked).
/// </summary>
public sealed class AzureDevOpsOptionsContributor : IOptionDefinitionQuery
{
    public string Section => Strings.AzureDevOps_Section;

    public IReadOnlyList<OptionDefinition> Options { get; } =
    [
        new(
            AzureDevOpsSettings.UrlKey,
            Strings.AzureDevOps_UrlLabel,
            OptionKind.Text,
            HintText: Strings.AzureDevOps_UrlHint),
        new(AzureDevOpsSettings.ProjectKey, Strings.AzureDevOps_ProjectLabel, OptionKind.Text),
        new(
            AzureDevOpsSettings.PatKey,
            Strings.AzureDevOps_PatLabel,
            OptionKind.Secret,
            HintText: Strings.AzureDevOps_PatHint),
        new(
            AzureDevOpsSettings.BookingElementFieldKey,
            Strings.AzureDevOps_BookingElementFieldLabel,
            OptionKind.Text,
            HintText: Strings.AzureDevOps_BookingElementFieldHint),
    ];
}
