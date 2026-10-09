using System.ComponentModel;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.ActivityMonitor.Models;

namespace Timetracker.Plugins.ActivityMonitor.ViewModels;

/// <summary>
/// Logic behind the monitor setup block in the options view: wraps the installer,
/// the status query and the process controller, and reports the button state, the
/// four-state monitor status and the result of the last action. Kept free of any
/// UI type so it can be tested without a running Avalonia application; the panel
/// only binds to its properties.
/// </summary>
public sealed class MonitorSetupViewModel : INotifyPropertyChanged
{
    private readonly IActivityMonitorInstaller _installer;
    private readonly IActivityMonitorStatusQuery _statusQuery;
    private readonly IActivityMonitorController _controller;

    private string _resultText = "";
    private bool _resultIsError;
    private bool _isBusy;
    private MonitorStatus _status;

    public MonitorSetupViewModel(
        IActivityMonitorInstaller installer,
        IActivityMonitorStatusQuery statusQuery,
        IActivityMonitorController controller)
    {
        _installer = installer;
        _statusQuery = statusQuery;
        _controller = controller;
        RefreshState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>True when Install should be offered (monitor not installed yet).</summary>
    public bool InstallEnabled => !_installer.IsInstalled;

    /// <summary>True when Remove should be offered (monitor already installed).</summary>
    public bool UninstallEnabled => _installer.IsInstalled;

    /// <summary>True when Start should be offered: installed, stopped and not busy.</summary>
    public bool StartEnabled => _installer.IsInstalled && Status == MonitorStatus.Stopped && !IsBusy;

    /// <summary>True when Stop should be offered: installed, running and not busy.</summary>
    public bool StopEnabled => _installer.IsInstalled && Status == MonitorStatus.Running && !IsBusy;

    /// <summary>The Start/Stop buttons appear only once the monitor is installed.</summary>
    public bool StartVisible => _installer.IsInstalled;

    /// <summary>The Start/Stop buttons appear only once the monitor is installed.</summary>
    public bool StopVisible => _installer.IsInstalled;

    /// <summary>True while a start or stop action is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(StartEnabled));
            OnPropertyChanged(nameof(StopEnabled));
        }
    }

    /// <summary>The monitor's current status (Uninstalled / Stopped / Running / Unknown).</summary>
    public MonitorStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StartEnabled));
            OnPropertyChanged(nameof(StopEnabled));
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

    /// <summary>Result of the last action; empty until the first one.</summary>
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
    /// Launches the monitor process. Start stays disabled until the next status
    /// refresh observes Running, so a second click cannot launch a duplicate
    /// process while the monitor is booting.
    /// </summary>
    public void Start()
    {
        IsBusy = true;
        var started = _controller.Start();
        ResultText = started
            ? Strings.ActivityMon_StartOk
            : string.Format(Strings.ActivityMon_StartFailed, _installer.MonitorExePath);
        ResultIsError = !started;
        if (!started)
        {
            // Nothing launched, so nothing is in flight; let Start be retried.
            IsBusy = false;
        }
        RefreshState();
    }

    /// <summary>Asks the monitor to stop gracefully and reports the result inline.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        var stopped = await _controller.StopAsync(cancellationToken);
        ResultText = stopped
            ? Strings.ActivityMon_StopOk
            : Strings.ActivityMon_StopFailed;
        ResultIsError = !stopped;
        IsBusy = false;
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
        // A fresh status resolves any in-flight start/stop, so clear the busy flag
        // regardless of the outcome; otherwise a monitor that crashes on boot would
        // leave Start/Stop permanently disabled.
        IsBusy = false;
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(InstallEnabled));
        OnPropertyChanged(nameof(UninstallEnabled));
        OnPropertyChanged(nameof(StartEnabled));
        OnPropertyChanged(nameof(StopEnabled));
        OnPropertyChanged(nameof(StartVisible));
        OnPropertyChanged(nameof(StopVisible));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
