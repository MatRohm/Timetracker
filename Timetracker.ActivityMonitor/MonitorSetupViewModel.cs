using System.ComponentModel;
using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Logic behind the monitor setup band: wraps the installer and reports the
/// resulting button state and status messages. Kept free of any UI type so it
/// can be tested without a running Avalonia application; the panel only binds
/// to its properties.
/// </summary>
public sealed class MonitorSetupViewModel : INotifyPropertyChanged
{
    private readonly IActivityMonitorInstaller _installer;
    private readonly IWeekStatusHost _statusHost;

    public MonitorSetupViewModel(
        IActivityMonitorInstaller installer, IWeekStatusHost statusHost)
    {
        _installer = installer;
        _statusHost = statusHost;
        RefreshState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>True when Install should be offered (monitor not installed yet).</summary>
    public bool InstallEnabled => !_installer.IsInstalled;

    /// <summary>True when Remove should be offered (monitor already installed).</summary>
    public bool UninstallEnabled => _installer.IsInstalled;

    /// <summary>Attempts the install and reports the result in the status line.</summary>
    public void Install()
    {
        var success = _installer.Install();
        _statusHost.ShowStatus(success
            ? "✓ The PC activity monitor will start automatically."
            : "✗ Installation failed. Make sure \"" + _installer.MonitorExePath
                + "\" exists next to the app.",
            success ? WeekStatusKind.Success : WeekStatusKind.Error);
        RefreshState();
    }

    /// <summary>Attempts the uninstall and reports the result in the status line.</summary>
    public void Uninstall()
    {
        var success = _installer.Uninstall();
        _statusHost.ShowStatus(success
            ? "✓ The PC activity monitor autostart was removed."
            : "✗ Removing the autostart entry failed.",
            success ? WeekStatusKind.Success : WeekStatusKind.Error);
        RefreshState();
    }

    /// <summary>Shows the current monitor state in the shared status line.</summary>
    public void ReportState() =>
        _statusHost.ShowStatus(_installer.IsInstalled
            ? "Monitoring is installed (starts automatically at logon)."
            : "The monitor is not installed; PC activity is only recorded while it runs.",
            _installer.IsInstalled ? WeekStatusKind.Success : WeekStatusKind.Info);

    private void RefreshState()
    {
        OnPropertyChanged(nameof(InstallEnabled));
        OnPropertyChanged(nameof(UninstallEnabled));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
