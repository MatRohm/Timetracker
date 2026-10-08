using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.ActivityMonitor.ViewModels;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Plugins.ActivityMonitor.Views;

/// <summary>
/// Setup block for the options view: install/remove buttons for the per-user
/// autostart, a status label (Uninstalled / Stopped / Running / Unknown) refreshed
/// periodically, and the result of the last action shown inline. Registered via
/// <see cref="MonitorSetupUiContributor"/>. View-only; state and button rules live
/// in <see cref="MonitorSetupViewModel"/>.
/// </summary>
public sealed class MonitorSetupPanel : UserControl
{
    private readonly MonitorSetupViewModel _viewModel;
    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();
    private readonly TextBlock _statusLabel = new();
    private readonly TextBlock _resultLabel = new();
    private readonly DispatcherTimer _refreshTimer;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button InstallButton => _installButton;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button UninstallButton => _uninstallButton;

    /// <summary>Exposed so tests can assert the four-state status text.</summary>
    public TextBlock StatusLabel => _statusLabel;

    /// <summary>Exposed so tests can assert the inline result text.</summary>
    public TextBlock ResultLabel => _resultLabel;

    public MonitorSetupPanel(IActivityMonitorInstaller installer, IActivityMonitorStatusQuery statusQuery)
    {
        _viewModel = new MonitorSetupViewModel(installer, statusQuery);

        _installButton.Content = Strings.ActivityMon_Install;
        _installButton.Click += (_, _) => _viewModel.Install();

        _uninstallButton.Content = Strings.ActivityMon_Remove;
        _uninstallButton.Click += (_, _) => _viewModel.Uninstall();

        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        _statusLabel.Foreground = ViewBrushes.Info;
        _resultLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _resultLabel.VerticalAlignment = VerticalAlignment.Center;

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _installButton, _uninstallButton, _statusLabel, _resultLabel },
        };

        _installButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.InstallEnabled)));
        _uninstallButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.UninstallEnabled)));

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Refresh the status once the block is on screen, then every five seconds.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await _viewModel.RefreshStatusAsync();
        AttachedToVisualTree += (_, _) =>
        {
            _ = _viewModel.RefreshStatusAsync();
            _refreshTimer.Start();
        };
        DetachedFromVisualTree += (_, _) => _refreshTimer.Stop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MonitorSetupViewModel.StatusText):
                _statusLabel.Text = _viewModel.StatusText;
                break;
            case nameof(MonitorSetupViewModel.ResultText):
                _resultLabel.Text = _viewModel.ResultText;
                break;
            case nameof(MonitorSetupViewModel.ResultIsError):
                _resultLabel.Foreground = _viewModel.ResultIsError ? ViewBrushes.Error : ViewBrushes.Success;
                break;
        }
    }

    private Avalonia.Data.Binding GetBinding(string propertyName) => new()
    {
        Source = _viewModel,
        Path = propertyName,
    };
}
