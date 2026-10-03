using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.Contracts.Interfaces;
using System.ComponentModel;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.ActivityMonitor.ViewModels;

/// <summary>
/// Logic behind the monitor setup band: wraps the installer and reports the
/// resulting button state and status messages. Kept free of any UI type so it
/// can be tested without a running Avalonia application; the panel only binds
/// to its properties.
/// </summary>
public sealed class MonitorSetupViewModel : INotifyPropertyChanged
{
    private readonly IActivityMonitorInstaller _installer;
    private readonly IWeekStatusCommand _statusHost;

    public MonitorSetupViewModel(
        IActivityMonitorInstaller installer, IWeekStatusCommand statusHost)
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
            ? Strings.ActivityMon_InstallOk
            : string.Format(Strings.ActivityMon_InstallFailed, _installer.MonitorExePath),
            success ? WeekStatusKind.Success : WeekStatusKind.Error);
        RefreshState();
    }

    /// <summary>Attempts the uninstall and reports the result in the status line.</summary>
    public void Uninstall()
    {
        var success = _installer.Uninstall();
        _statusHost.ShowStatus(success
            ? Strings.ActivityMon_UninstallOk
            : Strings.ActivityMon_UninstallFailed,
            success ? WeekStatusKind.Success : WeekStatusKind.Error);
        RefreshState();
    }

    /// <summary>Shows the current monitor state in the shared status line.</summary>
    public void ReportState() =>
        _statusHost.ShowStatus(_installer.IsInstalled
            ? Strings.ActivityMon_Installed
            : Strings.ActivityMon_NotInstalled,
            _installer.IsInstalled ? WeekStatusKind.Success : WeekStatusKind.Info);

    private void RefreshState()
    {
        OnPropertyChanged(nameof(InstallEnabled));
        OnPropertyChanged(nameof(UninstallEnabled));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
