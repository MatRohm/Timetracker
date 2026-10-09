using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.Models;
using Timetracker.Plugins.ActivityMonitor.ViewModels;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit.ViewModels;

[TestFixture]
public sealed class MonitorSetupViewModelTests
{
    [Test]
    public void InstallEnabled_WhenMonitorIsNotInstalled_ShouldBeTrue()
    {
        var viewModel = NewViewModel(isInstalled: false);

        viewModel.InstallEnabled.Should().BeTrue("not installed yet, so Install is offered");
        viewModel.UninstallEnabled.Should().BeFalse();
    }

    [Test]
    public void UninstallEnabled_WhenMonitorIsInstalled_ShouldBeTrue()
    {
        var viewModel = NewViewModel(isInstalled: true);

        viewModel.InstallEnabled.Should().BeFalse();
        viewModel.UninstallEnabled.Should().BeTrue("already installed, so Remove is offered");
    }

    [Test]
    public void Install_WhenInstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var viewModel = NewViewModel(isInstalled: false);

        viewModel.Install();

        viewModel.UninstallEnabled.Should().BeTrue("the install succeeded");
        viewModel.ResultIsError.Should().BeFalse("the result is reported as success");
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public void Install_WhenInstallFails_ShouldReportTheFailure()
    {
        var viewModel = new MonitorSetupViewModel(new FailingInstaller(), new FakeStatusQuery(), new FakeActivityMonitorController());

        viewModel.Install();

        viewModel.ResultIsError.Should().BeTrue("the result is reported as a failure");
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public void Uninstall_WhenUninstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var viewModel = NewViewModel(isInstalled: true);

        viewModel.Uninstall();

        viewModel.InstallEnabled.Should().BeTrue("the autostart was removed");
        viewModel.ResultIsError.Should().BeFalse();
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public async Task RefreshStatusAsync_WhenNotInstalled_ShouldReportUninstalled()
    {
        var viewModel = NewViewModel(isInstalled: false);

        await viewModel.RefreshStatusAsync();

        viewModel.Status.Should().Be(MonitorStatus.Uninstalled);
    }

    [Test]
    public async Task RefreshStatusAsync_WhenInstalledAndQuerySaysRunning_ShouldReportRunning()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Running);

        await viewModel.RefreshStatusAsync();

        viewModel.Status.Should().Be(MonitorStatus.Running);
    }

    [Test]
    public async Task RefreshStatusAsync_WhenInstalledAndQuerySaysStopped_ShouldReportStopped()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Stopped);

        await viewModel.RefreshStatusAsync();

        viewModel.Status.Should().Be(MonitorStatus.Stopped);
    }

    [Test]
    public async Task RefreshStatusAsync_WhenInstalledAndQuerySaysUnknown_ShouldReportUnknown()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Unknown);

        await viewModel.RefreshStatusAsync();

        viewModel.Status.Should().Be(MonitorStatus.Unknown);
    }

    [Test]
    public async Task StartEnabled_WhenInstalledAndStopped_ShouldBeTrue()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Stopped);
        await viewModel.RefreshStatusAsync();

        viewModel.StartEnabled.Should().BeTrue("installed and stopped, so Start is offered");
        viewModel.StopEnabled.Should().BeFalse();
    }

    [Test]
    public async Task StopEnabled_WhenInstalledAndRunning_ShouldBeTrue()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Running);
        await viewModel.RefreshStatusAsync();

        viewModel.StopEnabled.Should().BeTrue("installed and running, so Stop is offered");
        viewModel.StartEnabled.Should().BeFalse();
    }

    [Test]
    public async Task StartEnabled_WhenInstalledAndUnknown_ShouldBeFalse()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Unknown);
        await viewModel.RefreshStatusAsync();

        viewModel.StartEnabled.Should().BeFalse("an unknown state offers neither Start nor Stop");
        viewModel.StopEnabled.Should().BeFalse();
    }

    [Test]
    public void StartVisible_WhenNotInstalled_ShouldBeFalse()
    {
        var viewModel = NewViewModel(isInstalled: false);

        viewModel.StartVisible.Should().BeFalse("start/stop are hidden until the monitor is installed");
        viewModel.StopVisible.Should().BeFalse();
    }

    [Test]
    public void StartVisible_WhenInstalled_ShouldBeTrue()
    {
        var viewModel = NewViewModel(isInstalled: true);

        viewModel.StartVisible.Should().BeTrue();
        viewModel.StopVisible.Should().BeTrue();
    }

    [Test]
    public async Task StopAsync_WhenStopSucceeds_ShouldReportSuccess()
    {
        var viewModel = NewViewModel(isInstalled: true, status: MonitorStatus.Running);

        await viewModel.StopAsync();

        viewModel.ResultIsError.Should().BeFalse();
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public async Task StopAsync_WhenStopFails_ShouldReportFailure()
    {
        var viewModel = NewViewModel(
            isInstalled: true,
            status: MonitorStatus.Running,
            controller: new FakeActivityMonitorController { StopResult = false });

        await viewModel.StopAsync();

        viewModel.ResultIsError.Should().BeTrue();
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public async Task Start_WhenLaunchFails_ShouldReportFailureAndStayEnabled()
    {
        var viewModel = NewViewModel(
            isInstalled: true,
            status: MonitorStatus.Stopped,
            controller: new FakeActivityMonitorController { StartResult = false });
        await viewModel.RefreshStatusAsync();

        viewModel.Start();

        viewModel.ResultIsError.Should().BeTrue();
        viewModel.StartEnabled.Should().BeTrue("a failed launch leaves Start available to retry");
    }

    [Test]
    public async Task Start_WhenRunningIsObserved_ShouldOfferStop()
    {
        var query = new FakeStatusQuery { Status = MonitorStatus.Stopped };
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, query, new FakeActivityMonitorController());

        viewModel.Start();

        viewModel.StartEnabled.Should().BeFalse("Start stays disabled while the monitor boots");
        viewModel.StopEnabled.Should().BeFalse();

        query.Status = MonitorStatus.Running;
        await viewModel.RefreshStatusAsync();

        viewModel.StopEnabled.Should().BeTrue("once Running is observed, Stop is offered");
        viewModel.StartEnabled.Should().BeFalse();
    }

    [Test]
    public async Task Start_WhenRefreshObservesStopped_ShouldReenableStart()
    {
        var query = new FakeStatusQuery { Status = MonitorStatus.Stopped };
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, query, new FakeActivityMonitorController());

        viewModel.Start();

        viewModel.StartEnabled.Should().BeFalse("Start stays disabled while the monitor boots");

        query.Status = MonitorStatus.Stopped; // the monitor crashed / never came up
        await viewModel.RefreshStatusAsync();

        viewModel.StartEnabled.Should().BeTrue("a crashed boot re-enables Start so the user can retry");
    }

    private static MonitorSetupViewModel NewViewModel(
        bool isInstalled,
        MonitorStatus status = MonitorStatus.Unknown,
        FakeActivityMonitorController? controller = null) =>
        new(new InMemoryInstaller { IsInstalled = isInstalled }, new FakeStatusQuery { Status = status },
            controller ?? new FakeActivityMonitorController());

    /// <summary>An installer whose <see cref="IActivityMonitorInstaller.Install"/> always fails.</summary>
    private sealed class FailingInstaller : IActivityMonitorInstaller
    {
        public string MonitorExePath => "Timetracker.Plugins.ActivityMonitor";

        public bool IsInstalled => false;

        public bool Install() => false;

        public bool Uninstall() => true;
    }
}
