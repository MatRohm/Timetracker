using Timetracker.App.Interfaces;
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
                .Where(c => c.TargetTab == "Tracker")
                .Select(c => c.CreateControl(_services))
                .ToList();
            var weekContributors = _services.GetServices<IUiContributor>()
                .Where(c => c.TargetTab == "Week view")
                .Select(c => c.CreateControl(_services))
                .ToList();

            // Feed the per-day contributor lines (e.g. PC activity) into the week view.
            var weekDayContributors = _services.GetServices<IWeekDayContributor>().ToArray();
            viewModel.Week.DayContributorText = day =>
                string.Join(" · ", weekDayContributors
                    .Select(c => c.GetDayText(day))
                    .Where(text => text.Length > 0));

            // The day's active time (e.g. from the PC activity monitor) lets the week
            // view show what was not tracked. With several sources the longest wins,
            // since they measure the same computer use.
            var activitySources = _services.GetServices<IDayActivitySource>().ToArray();
            viewModel.Week.DayActiveTime = day =>
                activitySources
                    .Select(source => source.GetActiveTime(day))
                    .DefaultIfEmpty(TimeSpan.Zero)
                    .Max();

            // The same sources' active stretches let each day list its untracked gaps;
            // overlapping stretches of several sources are merged by the week view.
            viewModel.Week.DayActiveSpans = day =>
                [.. activitySources
                    .SelectMany(source => source.GetActiveSpans(day))
                    .Select(span => new Models.TimeRange(span.Start, span.End))];

            var window = new TrackerWindow(viewModel, trackerContributors, weekContributors);
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
