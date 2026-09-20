using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Contributes the monitor setup band to the week view: install/remove buttons
/// for the per-user autostart with a state label, reporting results through the
/// shared status line.
/// </summary>
public sealed class MonitorSetupUiContributor : IUiContributor
{
    public string TargetTab => "Week view";

    public Control CreateControl(IServiceProvider services) => new MonitorSetupPanel(
        services.GetRequiredService<IWeekStatusHost>());
}
