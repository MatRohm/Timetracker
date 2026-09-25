using Timetracker.Plugins.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Timetracker.AzureDevOps.Views;

/// <summary>
/// Registers the Azure DevOps import in the application UI: contributes its
/// panel to the tracker tab through the hook system.
/// </summary>
public sealed class AzureDevOpsUiContributor : IUiContributor
{
    public string TargetTab => "Tracker";

    public Avalonia.Controls.Control CreateControl(IServiceProvider services) =>
        new AzureDevOpsPanel(
            services.GetRequiredService<AzureDevOpsService>(),
            services.GetRequiredService<ITrackerUiHost>());
}
