using Timetracker.ActivityMonitor.Interfaces;
using Timetracker.Plugins.Interfaces;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Setup band for the week view: install/remove buttons for the per-user
/// autostart of the activity monitor. The current state and the results are
/// reported through the shared week status line at the bottom of the view.
/// Registered via <see cref="MonitorSetupUiContributor"/>. View-only; state and
/// button rules live in <see cref="MonitorSetupViewModel"/>.
/// </summary>
public sealed class MonitorSetupPanel : UserControl
{
    private readonly MonitorSetupViewModel _viewModel;
    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button InstallButton => _installButton;

    /// <summary>Exposed so tests can assert the button state.</summary>
    public Button UninstallButton => _uninstallButton;

    public MonitorSetupPanel(IActivityMonitorInstaller installer, IWeekStatusHost statusHost)
    {
        _viewModel = new MonitorSetupViewModel(installer, statusHost);

        _installButton.Content = "⏻ Install PC activity monitor";
        _installButton.Click += (_, _) => _viewModel.Install();

        _uninstallButton.Content = "⏻ Remove PC activity monitor";
        _uninstallButton.Click += (_, _) => _viewModel.Uninstall();

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _installButton, _uninstallButton },
        };

        _installButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.InstallEnabled)));
        _uninstallButton.Bind(IsEnabledProperty, GetBinding(nameof(MonitorSetupViewModel.UninstallEnabled)));

        // Report the current state once the view is actually attached.
        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(_viewModel.ReportState, DispatcherPriority.Background);
    }

    private Avalonia.Data.Binding GetBinding(string propertyName) => new()
    {
        Source = _viewModel,
        Path = propertyName,
    };
}
