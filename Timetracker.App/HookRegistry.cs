using Timetracker.App.Interfaces;
using Timetracker.Plugins.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Timetracker.App;

/// <summary>
/// Single place where every component registers itself with the DI container.
/// Each add-in adds its registrations here; the shell and the tab views resolve
/// everything else through <see cref="IServiceProvider"/>. Nothing else in the
/// app needs to know the concrete components.
/// </summary>
public static class HookRegistry
{
    /// <summary>Builds the application's service container.</summary>
    /// <param name="loggerFactory">
    /// Creates every component's <see cref="ILogger{TCategoryName}"/>. The app passes the
    /// file logging it set up before the container, so start-up failures are logged
    /// too; the caller owns and disposes it.
    /// </param>
    public static IServiceProvider BuildServiceProvider(ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();

        // Logging: ILogger<T> for every component, created by the given factory.
        services.AddSingleton(loggerFactory);
        services.AddLogging();

        // Framework services of the main app.
        // One repository instance is shared by every interface it implements.
        services.AddSingleton<Services.JsonTrackerRepository>();
        services.AddSingleton<ITrackerRepository>(
            sp => sp.GetRequiredService<Services.JsonTrackerRepository>());

        // Tracker file migrations: one step per version, chained by the migrator.
        services.AddSingleton<ITrackerFileMigration>(sp =>
            new Services.VersionOneToTwoMigration(
                sp.GetRequiredService<Services.JsonTrackerRepository>().FilePath));
        services.AddSingleton<ITrackerFileMigrationRunner>(sp =>
            new Services.TrackerFileMigrator(
                sp.GetRequiredService<Services.JsonTrackerRepository>().FilePath,
                sp.GetServices<ITrackerFileMigration>(),
                sp.GetRequiredService<ILogger<Services.TrackerFileMigrator>>()));
        services.AddSingleton<IUiTimer, Services.AvaloniaUiTimer>();
        // Idle detection comes from the monitor project's platform-specific provider.
        services.AddSingleton<Plugins.ActivityMonitor.Interfaces.IIdleTimeProvider>(
            _ => Plugins.ActivityMonitor.IdleTimeProvider.CreateForCurrentPlatform());
        services.AddSingleton<ViewModels.TrackerViewModel>();
        services.AddSingleton<ViewModels.WeekViewModel>();

        // Add-in registrations: one block per component.
        AddComponents(services);

        // Check at build time that every registered type can be constructed from its
        // dependencies (nothing is created yet), so a wiring mistake fails at startup
        // and in HookRegistryTests instead of on first use.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    /// <summary>
    /// Component registrations. Add-ins register the services they offer and the
    /// UI hooks they contribute; the app resolves the hook interfaces only.
    /// </summary>
    private static void AddComponents(IServiceCollection services)
    {
        // Add-in UI hosts: forwarded to the view models by the app's own services.
        services.AddSingleton<ITrackerUiHost, Services.TrackerUiHost>();
        services.AddSingleton<IWeekStatusHost, Services.WeekStatusHost>();

        // Azure DevOps import.
        services.AddSingleton<Plugins.AzureDevOps.AzureDevOpsConfig>(sp =>
            Plugins.AzureDevOps.AzureDevOpsConfig.Load(
                logger: sp.GetRequiredService<ILogger<Plugins.AzureDevOps.AzureDevOpsConfig>>()));
        services.AddSingleton<Plugins.AzureDevOps.AzureDevOpsService>(sp =>
            new Plugins.AzureDevOps.AzureDevOpsService(
                sp.GetRequiredService<Plugins.AzureDevOps.AzureDevOpsConfig>(),
                logger: sp.GetRequiredService<ILogger<Plugins.AzureDevOps.AzureDevOpsService>>()));
        services.AddSingleton<IUiContributor, Plugins.AzureDevOps.Views.AzureDevOpsUiContributor>();

        // PC activity monitor: activity log, per-day lines, installer UI.
        services.AddSingleton<Plugins.ActivityMonitor.ActivityLog>();
        services.AddSingleton<Plugins.ActivityMonitor.Interfaces.IActivityMonitorInstaller>(
            _ => Plugins.ActivityMonitor.ActivityMonitorInstallerFactory.CreateForCurrentPlatform());
        // One contributor instance serves the per-day line and the day's active time.
        services.AddSingleton<Plugins.ActivityMonitor.ActivityWeekDayContributor>();
        services.AddSingleton<IWeekDayContributor>(
            sp => sp.GetRequiredService<Plugins.ActivityMonitor.ActivityWeekDayContributor>());
        services.AddSingleton<IDayActivitySource>(
            sp => sp.GetRequiredService<Plugins.ActivityMonitor.ActivityWeekDayContributor>());
        services.AddSingleton<IUiContributor, Plugins.ActivityMonitor.Views.MonitorSetupUiContributor>();
    }
}
