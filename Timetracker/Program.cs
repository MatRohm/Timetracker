using Microsoft.Extensions.DependencyInjection;
using Timetracker.Services;
using Timetracker.Views;

namespace Timetracker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AttachGlobalErrorHandling();

        // Only one instance at a time, so entries are never written concurrently.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\TimetrackerApp", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Timetracker is already running.", "Timetracker",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();

            // Composition root: build the container; every component registers
            // itself in HookRegistry, the shell resolves only interfaces.
            var services = HookRegistry.BuildServiceProvider();

            using var viewModel = services.GetRequiredService<ViewModels.TrackerViewModel>();
            var form = new TrackerForm(viewModel, services);

            // Let the components run their startup hooks.
            foreach (var hook in services.GetServices<Plugins.IAppHook>())
            {
                hook.OnAppStarted(services);
            }

            Application.Run(form);

            foreach (var hook in services.GetServices<Plugins.IAppHook>())
            {
                hook.OnAppClosing();
            }
        }
        catch (Exception ex)
        {
            // Startup or message-loop exit failed hard.
            Report(ex, "Main");
        }
    }

    /// <summary>
    /// Routes every exception that breaks through to <see cref="Report"/>:
    /// a generic message is shown and the full details are written to the log.
    /// </summary>
    private static void AttachGlobalErrorHandling()
    {
        // WinForms UI thread: exceptions from event handlers/message loop.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception, "UI thread");

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

        ShowGenericError();
    }

    private static void ShowGenericError()
    {
        try
        {
            MessageBox.Show(
                "An unexpected error occurred. The application may not work correctly.\n\n" +
                "Details were written to the log:\n" + ErrorLog.FilePath,
                "Timetracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // The error reporter itself must never throw.
        }
    }
}
