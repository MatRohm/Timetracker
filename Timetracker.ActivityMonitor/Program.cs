using Microsoft.Win32;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Headless background monitor: starts the tracker, polls the idle state every
/// few seconds and closes the open span on logoff, shutdown or crash of the
/// session (SessionEnding). No UI; runs from the tray-less tray (invisible).
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var log = new ActivityLog();
        var tracker = new ActivityTracker(log);
        tracker.Start();

        // Logoff/shutdown: close the open span so the log stays consistent.
        SystemEvents.SessionEnding += (_, _) => tracker.Stop();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => tracker.Stop();

        var timer = new System.Windows.Forms.Timer { Interval = 5000 };
        timer.Tick += (_, _) => tracker.Poll();
        timer.Start();

        // Keep a message loop alive without a window (SystemEvents needs one).
        Application.Run(new HiddenForm(tracker, timer));
    }

    /// <summary>Never-shown form that keeps the message loop (and timer) running.</summary>
    private sealed class HiddenForm : Form
    {
        public HiddenForm(ActivityTracker tracker, System.Windows.Forms.Timer timer)
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            WindowState = FormWindowState.Minimized;

            // The form is never shown; closing ends the process.
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.ApplicationExitCall)
                {
                    return;
                }
                tracker.Stop();
                timer.Stop();
            };
        }
    }
}
