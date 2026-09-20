using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins;

namespace Timetracker.AzureDevOps;

/// <summary>
/// Registers the Azure DevOps import in the application UI: contributes its
/// panel to the tracker tab through the hook system.
/// </summary>
public sealed class AzureDevOpsUiContributor : IUiContributor
{
    public string TargetTab => "Tracker";

    public Control CreateControl(IServiceProvider services) =>
        new AzureDevOpsPanel(
            services.GetRequiredService<AzureDevOpsService>(),
            services.GetRequiredService<ITrackerUiHost>());
}
