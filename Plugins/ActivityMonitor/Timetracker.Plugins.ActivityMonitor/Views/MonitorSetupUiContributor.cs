using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Views;

/// <summary>
/// Contributes the monitor setup block to the "Activity monitor" options section:
/// install/remove buttons for the per-user autostart, with the result shown inline.
/// </summary>
public sealed class MonitorSetupUiContributor : IOptionUiQuery
{
    public string Section => Strings.ActivityMon_Section;

    public Avalonia.Controls.Control CreateControl(IServiceProvider services) =>
        new MonitorSetupPanel(
            services.GetRequiredService<IActivityMonitorInstaller>(),
            services.GetRequiredService<IActivityMonitorStatusQuery>(),
            services.GetRequiredService<IActivityMonitorController>());
}
