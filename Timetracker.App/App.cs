using Timetracker.Interfaces;
using Timetracker.Plugins.Contracts.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using Timetracker.Services;
using Timetracker.Views;

namespace Timetracker;

/// <summary>
/// Avalonia application entry point and composition root. Global error handling
/// is installed before the UI starts; the container is built and every
/// component's startup hook runs once the main window is shown.
/// </summary>
public sealed class App : Application
{
    /// <summary>Held for the process lifetime so the single-instance lock stays active.</summary>
    private Mutex? _singleInstanceMutex;

    private IServiceProvider? _services;

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
            AttachGlobalErrorHandling();

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
            _services = HookRegistry.BuildServiceProvider();

            // Upgrade an older tracker file to the current format before the first read.
            RunStartupMigrations(_services);

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
    private static void RunStartupMigrations(IServiceProvider services)
    {
        foreach (var runner in services.GetServices<ITrackerFileMigrationRunner>())
        {
            try
            {
                runner.MigrateIfNeeded();
            }
            catch (Exception ex)
            {
                Services.ErrorLog.Log("TrackerFileMigration", ex);
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
    /// Routes every exception that breaks through to <see cref="Report"/>:
    /// a generic message is shown and the full details are written to the log.
    /// </summary>
    private static void AttachGlobalErrorHandling()
    {
        // Non-UI threads and finalizer-observed failures.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject, "AppDomain");

        // Exceptions in unobserved tasks.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report(e.Exception, "Unobserved task");
            e.SetObserved();
        };
    }

    private static void Report(object? error, string context)
    {
        switch (error)
        {
            case Exception ex:
                ErrorLog.Log(context, ex);
                break;
            default:
                ErrorLog.Log(context, error?.ToString() ?? "Unknown error of unknown type");
                break;
        }
    }
}
