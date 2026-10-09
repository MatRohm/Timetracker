using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.ActivityMonitor.Models;
using Timetracker.Plugins.ActivityMonitor.ViewModels;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Plugins.ActivityMonitor.Views;

/// <summary>
/// Setup block for the options view: install/remove and start/stop buttons for the
/// per-user autostart, with the result of the last action shown inline, and a
/// status line below them with a traffic-light circle and the monitor's state
/// (Uninstalled / Stopped / Running / Unknown), refreshed periodically. Registered
/// via <see cref="MonitorSetupUiContributor"/>. View-only; state and button rules
/// live in <see cref="MonitorSetupViewModel"/>.
/// </summary>
public sealed class MonitorSetupPanel : UserControl
{
    private readonly MonitorSetupViewModel _viewModel;
    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Ellipse _statusLight = new();
    private readonly TextBlock _statusLabel = new();
    private readonly TextBlock _stateLabel = new();
    private readonly TextBlock _resultLabel = new();
    private readonly DispatcherTimer _refreshTimer;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button InstallButton => _installButton;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button UninstallButton => _uninstallButton;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button StartButton => _startButton;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button StopButton => _stopButton;

    /// <summary>Exposed so tests can assert the traffic-light color.</summary>
    public Ellipse StatusLight => _statusLight;

    /// <summary>Exposed so tests can assert the four-state status text.</summary>
    public TextBlock StatusLabel => _statusLabel;

    /// <summary>Exposed so tests can assert the state label text.</summary>
    public TextBlock StateLabel => _stateLabel;

    /// <summary>Exposed so tests can assert the inline result text.</summary>
    public TextBlock ResultLabel => _resultLabel;

    public MonitorSetupPanel(
        IActivityMonitorInstaller installer,
        IActivityMonitorStatusQuery statusQuery,
        IActivityMonitorController controller)
    {
        _viewModel = new MonitorSetupViewModel(installer, statusQuery, controller);

        _installButton.Content = Strings.ActivityMon_Install;
        _installButton.Click += (_, _) => _viewModel.Install();

        _uninstallButton.Content = Strings.ActivityMon_Remove;
        _uninstallButton.Click += (_, _) => _viewModel.Uninstall();

        _startButton.Content = Strings.ActivityMon_Start;
        _startButton.Click += (_, _) => _viewModel.Start();

        _stopButton.Content = Strings.ActivityMon_Stop;
        _stopButton.Click += (_, _) => StopSafely();

        _statusLight.Width = 12;
        _statusLight.Height = 12;
        _statusLight.VerticalAlignment = VerticalAlignment.Center;
        _statusLight.Fill = StatusBrush(_viewModel.Status);

        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        _statusLabel.Foreground = ViewBrushes.Info;
        _stateLabel.Text = Strings.ActivityMon_StateLabel;
        _stateLabel.VerticalAlignment = VerticalAlignment.Center;
        _stateLabel.Foreground = ViewBrushes.Info;
        _resultLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _resultLabel.VerticalAlignment = VerticalAlignment.Center;

        Content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { _installButton, _uninstallButton, _startButton, _stopButton, _resultLabel },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { _stateLabel, _statusLight, _statusLabel },
                },
            },
        };

        _installButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.InstallEnabled)));
        _uninstallButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.UninstallEnabled)));
        _startButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.StartEnabled)));
        _stopButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.StopEnabled)));
        _startButton.Bind(IsVisibleProperty, GetBinding(nameof(MonitorSetupViewModel.StartVisible)));
        _stopButton.Bind(IsVisibleProperty, GetBinding(nameof(MonitorSetupViewModel.StopVisible)));

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Refresh the status once the block is on screen, then every five seconds.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += (_, _) => RefreshStatusSafely();
        AttachedToVisualTree += (_, _) =>
        {
            RefreshStatusSafely();
            _refreshTimer.Start();
        };
        DetachedFromVisualTree += (_, _) => _refreshTimer.Stop();
    }

    /// <summary>Refreshes the status without ever crashing the UI on a failed call.</summary>
    private async void RefreshStatusSafely()
    {
        try
        {
            await _viewModel.RefreshStatusAsync();
        }
        catch (Exception)
        {
            // Keep the last status; the next tick retries.
        }
    }

    /// <summary>Stops the monitor without ever crashing the UI on a failed call.</summary>
    private async void StopSafely()
    {
        try
        {
            await _viewModel.StopAsync();
        }
        catch (Exception)
        {
            // Keep the last state; the next status tick reconciles.
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MonitorSetupViewModel.Status):
                _statusLight.Fill = StatusBrush(_viewModel.Status);
                break;
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

    /// <summary>The traffic-light color for a monitor state: green running, amber
    /// uninstalled, red unknown, grey stopped.</summary>
    private static IBrush StatusBrush(MonitorStatus status) => status switch
    {
        MonitorStatus.Running => ViewBrushes.Success,
        MonitorStatus.Uninstalled => ViewBrushes.Yellow,
        MonitorStatus.Unknown => ViewBrushes.Error,
        _ => ViewBrushes.Info,
    };

    private Avalonia.Data.Binding GetBinding(string propertyName) => new()
    {
        Source = _viewModel,
        Path = propertyName,
    };
}
