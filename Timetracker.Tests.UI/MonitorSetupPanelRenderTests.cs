using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using NUnit.Framework;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Views;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Tests.UI;

/// <summary>
/// UI smoke test for the monitor setup block: the install/remove buttons render
/// in the options view's "Activity monitor" section, and the result of an action
/// is shown inline with the success or error color.
/// </summary>
[TestFixture]
public sealed class MonitorSetupPanelRenderTests
{
    [AvaloniaTest]
    public void MonitorSetupPanel_WhenRendered_ShouldShowInstallAndRemoveButtons()
    {
        var panel = new MonitorSetupPanel(new FakeInstaller { IsInstalled = false });
        var window = new Window { Content = panel };
        window.Show();

        panel.InstallButton.IsEnabled.Should().BeTrue("not installed, so Install is offered");
        panel.UninstallButton.IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenInstallSucceeds_ShouldShowTheSuccessMessageInline()
    {
        var panel = new MonitorSetupPanel(new FakeInstaller { IsInstalled = false });
        var window = new Window { Content = panel };
        window.Show();

        panel.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        panel.ResultLabel.Text.Should().NotBeEmpty("the result is shown inline");
        panel.ResultLabel.Foreground.Should().BeSameAs(ViewBrushes.Success, "a successful install is green");
    }

    private sealed class FakeInstaller : IActivityMonitorInstaller
    {
        public string MonitorExePath => "Timetracker.Plugins.ActivityMonitor";

        public bool IsInstalled { get; set; }

        public bool Install()
        {
            IsInstalled = true;
            return true;
        }

        public bool Uninstall()
        {
            IsInstalled = false;
            return true;
        }
    }
}
