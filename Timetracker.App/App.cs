using Timetracker.App.Interfaces;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Timetracker.App.Views;
using Timetracker.Plugins.Contracts.Logging;

namespace Timetracker.App;

/// <summary>
/// Avalonia application entry point and composition root. File logging and global
/// error handling are installed before the UI starts; the container is built and every
/// component's startup hook runs once the main window is shown.
/// </summary>
public sealed class App : Application
{
    /// <summary>Held for the process lifetime so the single-instance lock stays active.</summary>
    private Mutex? _singleInstanceMutex;

    private IServiceProvider? _services;

    /// <summary>The process's file logging; flushed and closed when the app exits.</summary>
    private ILoggerFactory? _loggerFactory;

    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

        // The DataGrid ships its theme separately from the core controls.
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddTimetrackerFile("app"));
            desktop.Exit += (_, _) => _loggerFactory.Dispose();
            var logger = _loggerFactory.CreateLogger<App>();
            AttachGlobalErrorHandling(logger);

            // Only one instance at a time, so entries are never written concurrently.
            var mutexName = OperatingSystem.IsWindows()
                ? @"Local\TimetrackerApp"
                : "TimetrackerApp";
            _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out var isFirstInstance);
            if (!isFirstInstance)
            {
                ShowAlreadyRunning(desktop);
                return;
            }

            // Composition root: build the container; every component registers
            // itself in HookRegistry, the shell resolves only interfaces.
            _services = HookRegistry.BuildServiceProvider(_loggerFactory);

            // Upgrade an older tracker file to the current format before the first read.
            RunStartupMigrations(_services, logger);

            var viewModel = _services.GetRequiredService<ViewModels.TrackerViewModel>();

            // Resolve the add-in UI contributors and build their controls here, so
            // the tab views stay free of the plugin contract.
            var trackerContributors = _services.GetServices<IUiContributor>()
                .Where(c => c.TargetTab == TabKeys.Tracker)
                .Select(c => c.CreateControl(_services))
                .ToList();

            // Each plugin tab builds its content and receives the add-in controls
            // targeted at its tab key, ordered between the tracker and options tabs.
            var pluginTabs = _services.GetServices<ITabContributor>()
                .OrderBy(t => t.Order)
                .Select(t => new TabItem
                {
                    Header = t.Header,
                    Content = t.CreateView(
                        _services.GetServices<IUiContributor>()
                            .Where(c => c.TargetTab == t.TabKey)
                            .Select(c => c.CreateControl(_services))
                            .ToList()),
                })
                .ToList();

            var window = new TrackerWindow(
                viewModel,
                trackerContributors,
                pluginTabs,
                _services.GetRequiredService<ViewModels.OptionsViewModel>());
            desktop.MainWindow = window;

            // Let the components run their startup hooks.
            foreach (var hook in _services.GetServices<IAppHook>())
            {
                hook.OnAppStarted(_services);
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                foreach (var hook in _services.GetServices<IAppHook>())
                {
                    hook.OnAppClosing();
                }
                viewModel.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Runs every registered file migration once, before any component reads the
    /// data file. A failure is ignored so a broken file can never stop startup.
    /// </summary>
    private static void RunStartupMigrations(IServiceProvider services, ILogger logger)
    {
        foreach (var runner in services.GetServices<ITrackerFileMigrationRunner>())
        {
            try
            {
                runner.MigrateIfNeeded();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The tracker file migration failed");
            }
        }
    }

    /// <summary>Explains that another instance owns the data file and shuts this one down.</summary>
    private static void ShowAlreadyRunning(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var window = new Window
        {
            Title = "Timetracker",
            Width = 360,
            Height = 130,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Avalonia.Controls.TextBlock
            {
                Text = "Timetracker is already running.",
                Margin = new Avalonia.Thickness(16),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        };

        // Closing the notice (or startup finishing) ends this instance.
        window.Closed += (_, _) => desktop.Shutdown();
        desktop.MainWindow = window;
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    /// <summary>
    /// Writes every exception that breaks through to the log with its full details.
    /// </summary>
    private static void AttachGlobalErrorHandling(ILogger logger)
    {
        // Non-UI threads and finalizer-observed failures.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception");
            }
            else
            {
                logger.LogCritical("Unhandled error of unknown type: {Error}", e.ExceptionObject);
            }
        };

        // Exceptions in unobserved tasks.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }
}
