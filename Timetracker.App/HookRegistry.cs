using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins;

namespace Timetracker;

/// <summary>
/// Single place where every component registers itself with the DI container.
/// Each add-in adds its registrations here; the shell and the tab views resolve
/// everything else through <see cref="IServiceProvider"/>. Nothing else in the
/// app needs to know the concrete components.
/// </summary>
public static class HookRegistry
{
    /// <summary>Builds the application's service container.</summary>
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Framework services of the main app.
        // One repository instance is shared by every interface it implements.
        services.AddSingleton<Services.JsonTrackerRepository>();
        services.AddSingleton<Services.ITrackerRepository>(
            sp => sp.GetRequiredService<Services.JsonTrackerRepository>());

        // Tracker file migrations: one step per version, chained by the migrator.
        services.AddSingleton<Services.ITrackerFileMigration>(sp =>
            new Services.VersionOneToTwoMigration(
                sp.GetRequiredService<Services.JsonTrackerRepository>().FilePath));
        services.AddSingleton<Services.ITrackerFileMigrationRunner>(sp =>
            new Services.TrackerFileMigrator(
                sp.GetRequiredService<Services.JsonTrackerRepository>().FilePath,
                sp.GetServices<Services.ITrackerFileMigration>(),
                Services.ErrorLog.Log));
        services.AddSingleton<ViewModels.IUiTimer, Views.AvaloniaUiTimer>();
        // Idle detection comes from the monitor project's platform-specific provider.
        services.AddSingleton<ActivityMonitor.IIdleTimeProvider>(
            _ => ActivityMonitor.IdleTimeProvider.CreateForCurrentPlatform());
        services.AddSingleton<ViewModels.TrackerViewModel>();
        services.AddSingleton<ViewModels.WeekViewModel>();

        // Add-in registrations: one block per component.
        AddComponents(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Component registrations. Add-ins register the services they offer and the
    /// UI hooks they contribute; the app resolves the hook interfaces only.
    /// </summary>
    private static void AddComponents(IServiceCollection services)
    {
        // Azure DevOps import.
        services.AddSingleton<AzureDevOps.AzureDevOpsConfig>(_ =>
            AzureDevOps.AzureDevOpsConfig.Load());
        services.AddSingleton<AzureDevOps.AzureDevOpsService>(sp =>
            new AzureDevOps.AzureDevOpsService(
                sp.GetRequiredService<AzureDevOps.AzureDevOpsConfig>()));
        services.AddSingleton<ITrackerUiHost>(_ => UiHostAccessor.GetTrackerHost<ITrackerUiHost>());
        services.AddSingleton<IUiContributor, AzureDevOps.AzureDevOpsUiContributor>();

        // PC activity monitor: activity log, per-day lines, installer UI.
        services.AddSingleton<ActivityMonitor.ActivityLog>();
        services.AddSingleton<ActivityMonitor.IActivityMonitorInstaller>(
            _ => ActivityMonitor.ActivityMonitorInstallerFactory.CreateForCurrentPlatform());
        services.AddSingleton<IWeekDayContributor, ActivityMonitor.ActivityWeekDayContributor>();
        services.AddSingleton<IUiContributor, ActivityMonitor.MonitorSetupUiContributor>();
        services.AddSingleton<Plugins.IWeekStatusHost, ActivityMonitor.WeekStatusHostAdapter>();
    }
}
