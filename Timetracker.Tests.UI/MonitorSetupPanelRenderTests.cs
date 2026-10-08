using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Models;
using Timetracker.Plugins.ActivityMonitor.Views;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Tests.UI;

/// <summary>
/// UI smoke test for the monitor setup block: the install/remove buttons render
/// in the options view's "Activity monitor" section, the four-state status is
/// shown, and the result of an action is shown inline with the success or error
/// color.
/// </summary>
[TestFixture]
public sealed class MonitorSetupPanelRenderTests
{
    [AvaloniaTest]
    public void MonitorSetupPanel_WhenRendered_ShouldShowInstallAndRemoveButtons()
    {
        var panel = new MonitorSetupPanel(new FakeInstaller { IsInstalled = false }, new FakeStatusQuery());
        var window = new Window { Content = panel };
        window.Show();

        panel.InstallButton.IsEnabled.Should().BeTrue("not installed, so Install is offered");
        panel.UninstallButton.IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenInstallSucceeds_ShouldShowTheSuccessMessageInline()
    {
        var panel = new MonitorSetupPanel(new FakeInstaller { IsInstalled = false }, new FakeStatusQuery());
        var window = new Window { Content = panel };
        window.Show();

        panel.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        panel.ResultLabel.Text.Should().NotBeEmpty("the result is shown inline");
        panel.ResultLabel.Foreground.Should().BeSameAs(ViewBrushes.Success, "a successful install is green");
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenRunning_ShouldLightGreen()
    {
        var panel = Realize(new FakeInstaller { IsInstalled = true }, new FakeStatusQuery { Status = MonitorStatus.Running });

        panel.StatusLight.Fill.Should().BeSameAs(ViewBrushes.Success);
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenUninstalled_ShouldLightYellow()
    {
        var panel = Realize(new FakeInstaller { IsInstalled = false }, new FakeStatusQuery());

        panel.StatusLight.Fill.Should().BeSameAs(ViewBrushes.Yellow);
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenStopped_ShouldLightGrey()
    {
        var panel = Realize(new FakeInstaller { IsInstalled = true }, new FakeStatusQuery { Status = MonitorStatus.Stopped });

        panel.StatusLight.Fill.Should().BeSameAs(ViewBrushes.Info);
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenUnknown_ShouldLightRed()
    {
        var panel = Realize(new FakeInstaller { IsInstalled = true }, new FakeStatusQuery { Status = MonitorStatus.Unknown });

        panel.StatusLight.Fill.Should().BeSameAs(ViewBrushes.Error);
    }

    [AvaloniaTest]
    public void MonitorSetupPanel_WhenRendered_ShouldShowTheStatusBelowTheButtons()
    {
        var panel = Realize(new FakeInstaller { IsInstalled = true }, new FakeStatusQuery { Status = MonitorStatus.Running });

        var buttonsTop = panel.InstallButton.TranslatePoint(default, panel)!.Value.Y;
        var statusTop = panel.StatusLight.TranslatePoint(default, panel)!.Value.Y;
        statusTop.Should().BeGreaterThan(buttonsTop, "the status sits on its own line below the buttons");
    }

    private static MonitorSetupPanel Realize(IActivityMonitorInstaller installer, IActivityMonitorStatusQuery query)
    {
        var panel = new MonitorSetupPanel(installer, query);
        var window = new Window { Content = panel, Width = 900, Height = 500 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return panel;
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

    private sealed class FakeStatusQuery : IActivityMonitorStatusQuery
    {
        public MonitorStatus Status { get; set; } = MonitorStatus.Unknown;

        public Task<MonitorStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Status);
    }
}
