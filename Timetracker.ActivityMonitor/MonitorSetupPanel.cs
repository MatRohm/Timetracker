using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Setup band for the week view: install/remove buttons for the per-user
/// autostart of the activity monitor. The current state and the results are
/// reported through the shared week status line at the bottom of the view.
/// Registered via <see cref="MonitorSetupUiContributor"/>.
/// </summary>
public sealed class MonitorSetupPanel : UserControl
{
    private readonly IWeekStatusHost _statusHost;

    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();

    public MonitorSetupPanel(IWeekStatusHost statusHost)
    {
        _statusHost = statusHost;
        // Size the panel to its buttons; the default UserControl size would
        // clip the remove button in the auto-sized contributor row.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0);
        Padding = new Padding(0);

        BuildUi();
        UpdateButtons();
    }

    /// <summary>Reports the current monitor state once the view is actually shown.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ReportState();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _installButton.Text = "⏻ Install PC activity monitor";
        _installButton.AutoSize = true;
        _installButton.MinimumSize = new Size(200, 30);
        _installButton.Margin = new Padding(0, 0, 8, 6);

        _uninstallButton.Text = "⏻ Remove PC activity monitor";
        _uninstallButton.AutoSize = true;
        _uninstallButton.MinimumSize = new Size(210, 30);
        _uninstallButton.Margin = new Padding(0, 0, 0, 6);

        layout.Controls.Add(_installButton, 0, 0);
        layout.Controls.Add(_uninstallButton, 1, 0);

        _installButton.Click += OnInstallClicked;
        _uninstallButton.Click += OnUninstallClicked;

        Controls.Add(layout);
    }

    private void OnInstallClicked(object? sender, EventArgs e)
    {
        var success = ActivityMonitorInstaller.Install();
        _statusHost.ShowStatus(success
            ? "✓ The PC activity monitor will start with Windows."
            : "✗ Installation failed. Make sure \"" + ActivityMonitorInstaller.MonitorExePath
                + "\" exists next to the app.",
            success ? WeekStatusKind.Success : WeekStatusKind.Error);
        UpdateButtons();
    }

    private void OnUninstallClicked(object? sender, EventArgs e)
    {
        var success = ActivityMonitorInstaller.Uninstall();
        _statusHost.ShowStatus(success
            ? "✓ The PC activity monitor autostart was removed."
            : "✗ Removing the autostart entry failed.",
            success ? WeekStatusKind.Success : WeekStatusKind.Error);
        UpdateButtons();
    }

    /// <summary>Enables exactly the button that matches the autostart state.</summary>
    private void UpdateButtons()
    {
        var installed = ActivityMonitorInstaller.IsInstalled;
        _installButton.Enabled = !installed;
        _uninstallButton.Enabled = installed;
    }

    /// <summary>Shows the current monitor state in the shared status line at the bottom.</summary>
    private void ReportState()
    {
        var installed = ActivityMonitorInstaller.IsInstalled;
        _statusHost.ShowStatus(installed
            ? "Monitoring is installed (starts with Windows)."
            : "The monitor is not installed; PC activity is only recorded while it runs.",
            installed ? WeekStatusKind.Success : WeekStatusKind.Info);
    }
}
