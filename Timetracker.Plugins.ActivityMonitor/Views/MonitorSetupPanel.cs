using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.ActivityMonitor.ViewModels;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Plugins.ActivityMonitor.Views;

/// <summary>
/// Setup block for the options view: install/remove buttons for the per-user
/// autostart, with the result of the last action shown inline next to them.
/// Registered via <see cref="MonitorSetupUiContributor"/>. View-only; state and
/// button rules live in <see cref="MonitorSetupViewModel"/>.
/// </summary>
public sealed class MonitorSetupPanel : UserControl
{
    private readonly MonitorSetupViewModel _viewModel;
    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();
    private readonly TextBlock _resultLabel = new();

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button InstallButton => _installButton;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button UninstallButton => _uninstallButton;

    /// <summary>Exposed so tests can assert the inline result text.</summary>
    public TextBlock ResultLabel => _resultLabel;

    public MonitorSetupPanel(IActivityMonitorInstaller installer)
    {
        _viewModel = new MonitorSetupViewModel(installer);

        _installButton.Content = Strings.ActivityMon_Install;
        _installButton.Click += (_, _) => _viewModel.Install();

        _uninstallButton.Content = Strings.ActivityMon_Remove;
        _uninstallButton.Click += (_, _) => _viewModel.Uninstall();

        _resultLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _resultLabel.VerticalAlignment = VerticalAlignment.Center;

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _installButton, _uninstallButton, _resultLabel },
        };

        _installButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.InstallEnabled)));
        _uninstallButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.UninstallEnabled)));

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorSetupViewModel.ResultText))
        {
            _resultLabel.Text = _viewModel.ResultText;
        }
        else if (e.PropertyName == nameof(MonitorSetupViewModel.ResultIsError))
        {
            _resultLabel.Foreground = _viewModel.ResultIsError ? ViewBrushes.Error : ViewBrushes.Success;
        }
    }

    private Avalonia.Data.Binding GetBinding(string propertyName) => new()
    {
        Source = _viewModel,
        Path = propertyName,
    };
}
