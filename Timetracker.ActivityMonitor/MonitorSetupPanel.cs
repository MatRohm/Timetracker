using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Setup band for the week view: install/remove buttons for the per-user
/// autostart of the activity monitor, with a state label. Results are reported
/// through the shared week status line. Registered via
/// <see cref="MonitorSetupUiContributor"/>.
/// </summary>
public sealed class MonitorSetupPanel : UserControl
{
    private readonly IWeekStatusHost _statusHost;

    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();
    private readonly Label _stateLabel = new();

    public MonitorSetupPanel(IWeekStatusHost statusHost)
    {
        _statusHost = statusHost;
        Dock = DockStyle.Fill;

        BuildUi();
        UpdateButtons();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _installButton.Text = "⏻ Install PC activity monitor";
        _installButton.AutoSize = true;
        _installButton.MinimumSize = new Size(200, 30);
        _installButton.Margin = new Padding(0, 0, 8, 6);

        _uninstallButton.Text = "⏻ Remove PC activity monitor";
        _uninstallButton.AutoSize = true;
        _uninstallButton.MinimumSize = new Size(210, 30);
        _uninstallButton.Margin = new Padding(0, 0, 8, 6);

        _stateLabel.AutoSize = false;
        _stateLabel.ForeColor = Color.DimGray;
        _stateLabel.Dock = DockStyle.Fill;
        _stateLabel.TextAlign = ContentAlignment.MiddleLeft;
        _stateLabel.Margin = new Padding(0, 6, 0, 6);

        layout.Controls.Add(_installButton, 0, 0);
        layout.Controls.Add(_uninstallButton, 1, 0);
        layout.Controls.Add(_stateLabel, 2, 0);

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
                + "\" exists next to the app.", success);
        UpdateButtons();
    }

    private void OnUninstallClicked(object? sender, EventArgs e)
    {
        var success = ActivityMonitorInstaller.Uninstall();
        _statusHost.ShowStatus(success
            ? "✓ The PC activity monitor autostart was removed."
            : "✗ Removing the autostart entry failed.", success);
        UpdateButtons();
    }

    /// <summary>Enables exactly the button that matches the autostart state.</summary>
    private void UpdateButtons()
    {
        var installed = ActivityMonitorInstaller.IsInstalled;
        _installButton.Enabled = !installed;
        _uninstallButton.Enabled = installed;
        _stateLabel.Text = installed
            ? "Monitoring is installed (starts with Windows)."
            : "The monitor is not installed; PC activity is only recorded while it runs.";
    }
}
