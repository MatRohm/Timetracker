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
        services.AddSingleton<ITrackerFileMigration>(sp =>
            new Services.VersionTwoToThreeMigration(
                sp.GetRequiredService<Services.JsonTrackerRepository>().FilePath));
        services.AddSingleton<ITrackerFileMigrationRunner>(sp =>
            new Services.TrackerFileMigrator(
                sp.GetRequiredService<Services.JsonTrackerRepository>().FilePath,
                sp.GetServices<ITrackerFileMigration>(),
                sp.GetRequiredService<ILogger<Services.TrackerFileMigrator>>()));
        services.AddSingleton<IUiTimer, Services.AvaloniaUiTimer>();
        services.AddSingleton<Services.EntryEditor>(sp =>
            new Services.EntryEditor(
                sp.GetRequiredService<ITrackerRepository>().SaveAsync,
                sp.GetRequiredService<ILogger<Services.EntryEditor>>()));
        services.AddSingleton<ViewModels.TrackerViewModel>();

        // Options: one store for every component, and the options tab with the app's
        // own section first (sections appear in registration order).
        services.AddSingleton<Services.JsonOptionsStore>(sp =>
            new Services.JsonOptionsStore(logger: sp.GetRequiredService<ILogger<Services.JsonOptionsStore>>()));
        services.AddSingleton<IOptionQuery>(sp => sp.GetRequiredService<Services.JsonOptionsStore>());
        services.AddSingleton<IOptionCommand>(sp => sp.GetRequiredService<Services.JsonOptionsStore>());
        services.AddSingleton<IOptionDefinitionQuery>(sp => new Services.GeneralOptionsContributor(
            sp.GetRequiredService<Services.JsonTrackerRepository>(),
            sp.GetRequiredService<Services.JsonOptionsStore>()));
        services.AddSingleton<IFileExplorer, Services.FileExplorer>();
        services.AddSingleton<ViewModels.OptionsViewModel>();

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
        services.AddSingleton<ITrackerUiCommand, Services.TrackerUiHost>();
        // One session-host instance serves both its query and command surface.
        services.AddSingleton<Services.TrackerSessionHost>();
        services.AddSingleton<ITrackerSessionQuery>(sp => sp.GetRequiredService<Services.TrackerSessionHost>());
        services.AddSingleton<ITrackerSessionCommand>(sp => sp.GetRequiredService<Services.TrackerSessionHost>());
        // One tracked-sessions instance serves both its query and command surface, so
        // the Changed event and the booked changes stay on the same object.
        services.AddSingleton<Services.TrackedSessionsHost>();
        services.AddSingleton<ITrackedSessionsQuery>(sp => sp.GetRequiredService<Services.TrackedSessionsHost>());
        services.AddSingleton<ITrackedSessionsCommand>(sp => sp.GetRequiredService<Services.TrackedSessionsHost>());

        // Week view plugin: its tab, its status line, and its view model.
        services.AddSingleton<Plugins.WeekView.ViewModels.WeekViewModel>();
        services.AddSingleton<IWeekStatusCommand, Plugins.WeekView.Services.WeekStatusHost>();
        services.AddSingleton<ITabQuery, Plugins.WeekView.WeekTabContributor>();

        // Azure DevOps import: the connection is edited in the options tab.
        services.AddSingleton<Plugins.AzureDevOps.AzureDevOpsSettings>();
        services.AddSingleton<Plugins.AzureDevOps.AzureDevOpsService>(sp =>
            new Plugins.AzureDevOps.AzureDevOpsService(
                sp.GetRequiredService<Plugins.AzureDevOps.AzureDevOpsSettings>().Current,
                logger: sp.GetRequiredService<ILogger<Plugins.AzureDevOps.AzureDevOpsService>>()));
        services.AddSingleton<IOptionDefinitionQuery, Plugins.AzureDevOps.AzureDevOpsOptionsContributor>();
        services.AddSingleton<IUiQuery, Plugins.AzureDevOps.Views.AzureDevOpsUiContributor>();

        // PC activity monitor: activity log, per-day lines, installer UI.
        services.AddSingleton<Plugins.ActivityMonitor.Services.ActivityLog>();
        services.AddSingleton<Plugins.ActivityMonitor.Interfaces.IActivityMonitorInstaller>(
            _ => Plugins.ActivityMonitor.Services.ActivityMonitorInstallerFactory.CreateForCurrentPlatform());
        // The running-state query talks to the monitor's gRPC status endpoint.
        services.AddSingleton<Plugins.ActivityMonitor.Interfaces.IActivityMonitorStatusQuery,
            Plugins.ActivityMonitor.Services.GrpcMonitorStatusQuery>();
        // Idle detection comes from the monitor project's platform-specific provider.
        services.AddSingleton<Plugins.ActivityMonitor.Interfaces.IIdleTimeProvider>(
            _ => Plugins.ActivityMonitor.Services.IdleTimeProvider.CreateForCurrentPlatform());
        // The idle auto-stop rule: stops the tracker's session after a long idle stretch,
        // with the idle-stop threshold the user configured in the options tab.
        services.AddSingleton<Plugins.ActivityMonitor.Services.IdleAutoStop>(sp =>
            new Plugins.ActivityMonitor.Services.IdleAutoStop(
                sp.GetRequiredService<ITrackerSessionQuery>(),
                sp.GetRequiredService<ITrackerSessionCommand>(),
                sp.GetRequiredService<Plugins.ActivityMonitor.Interfaces.IIdleTimeProvider>(),
                options: sp.GetRequiredService<IOptionQuery>()));
        services.AddSingleton<IAppCommand>(sp =>
            sp.GetRequiredService<Plugins.ActivityMonitor.Services.IdleAutoStop>());
        // One contributor instance serves the per-day line and the day's active time.
        services.AddSingleton<Plugins.ActivityMonitor.Services.ActivityWeekDayContributor>();
        services.AddSingleton<IWeekDayQuery>(
            sp => sp.GetRequiredService<Plugins.ActivityMonitor.Services.ActivityWeekDayContributor>());
        services.AddSingleton<IDayActivityQuery>(
            sp => sp.GetRequiredService<Plugins.ActivityMonitor.Services.ActivityWeekDayContributor>());
        services.AddSingleton<IOptionUiQuery, Plugins.ActivityMonitor.Views.MonitorSetupUiContributor>();
        services.AddSingleton<IOptionDefinitionQuery, Plugins.ActivityMonitor.Services.ActivityOptionsContributor>();
    }
}
