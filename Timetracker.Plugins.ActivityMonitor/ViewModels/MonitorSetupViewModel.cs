using System.ComponentModel;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.ActivityMonitor.Models;

namespace Timetracker.Plugins.ActivityMonitor.ViewModels;

/// <summary>
/// Logic behind the monitor setup block in the options view: wraps the installer
/// and the status query, and reports the button state, the four-state monitor
/// status and the result of the last install/remove action. Kept free of any UI
/// type so it can be tested without a running Avalonia application; the panel
/// only binds to its properties.
/// </summary>
public sealed class MonitorSetupViewModel : INotifyPropertyChanged
{
    private readonly IActivityMonitorInstaller _installer;
    private readonly IActivityMonitorStatusQuery _statusQuery;

    private string _resultText = "";
    private bool _resultIsError;
    private MonitorStatus _status;

    public MonitorSetupViewModel(
        IActivityMonitorInstaller installer,
        IActivityMonitorStatusQuery statusQuery)
    {
        _installer = installer;
        _statusQuery = statusQuery;
        RefreshState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>True when Install should be offered (monitor not installed yet).</summary>
    public bool InstallEnabled => !_installer.IsInstalled;

    /// <summary>True when Remove should be offered (monitor already installed).</summary>
    public bool UninstallEnabled => _installer.IsInstalled;

    /// <summary>The monitor's current status (Uninstalled / Stopped / Running / Unknown).</summary>
    public MonitorStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>The localized label for <see cref="Status"/>.</summary>
    public string StatusText => Status switch
    {
        MonitorStatus.Uninstalled => Strings.ActivityMon_StatusUninstalled,
        MonitorStatus.Stopped => Strings.ActivityMon_StatusStopped,
        MonitorStatus.Running => Strings.ActivityMon_StatusRunning,
        _ => Strings.ActivityMon_StatusUnknown,
    };

    /// <summary>Result of the last install/remove action; empty until the first one.</summary>
    public string ResultText
    {
        get => _resultText;
        private set
        {
            _resultText = value;
            OnPropertyChanged(nameof(ResultText));
        }
    }

    /// <summary>True when <see cref="ResultText"/> is a failure, so the view colors it red.</summary>
    public bool ResultIsError
    {
        get => _resultIsError;
        private set
        {
            _resultIsError = value;
            OnPropertyChanged(nameof(ResultIsError));
        }
    }

    /// <summary>Attempts the install and reports the result inline.</summary>
    public void Install()
    {
        var success = _installer.Install();
        ResultText = success
            ? Strings.ActivityMon_InstallOk
            : string.Format(Strings.ActivityMon_InstallFailed, _installer.MonitorExePath);
        ResultIsError = !success;
        RefreshState();
    }

    /// <summary>Attempts the uninstall and reports the result inline.</summary>
    public void Uninstall()
    {
        var success = _installer.Uninstall();
        ResultText = success
            ? Strings.ActivityMon_UninstallOk
            : Strings.ActivityMon_UninstallFailed;
        ResultIsError = !success;
        RefreshState();
    }

    /// <summary>
    /// Re-reads the monitor status: Uninstalled when there is no autostart entry,
    /// otherwise the running state reported by the status query.
    /// </summary>
    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_installer.IsInstalled)
        {
            Status = MonitorStatus.Uninstalled;
            return;
        }

        var status = await _statusQuery.GetStatusAsync(cancellationToken);
        Status = status;
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(InstallEnabled));
        OnPropertyChanged(nameof(UninstallEnabled));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
