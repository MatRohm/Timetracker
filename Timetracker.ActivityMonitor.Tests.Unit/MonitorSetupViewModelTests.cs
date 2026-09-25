using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class MonitorSetupViewModelTests
{
    [Test]
    public void InstallEnabled_WhenMonitorIsNotInstalled_ShouldBeTrue()
    {
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = false }, new FakeWeekStatusHost());

        viewModel.InstallEnabled.Should().BeTrue("not installed yet, so Install is offered");
        viewModel.UninstallEnabled.Should().BeFalse();
    }

    [Test]
    public void UninstallEnabled_WhenMonitorIsInstalled_ShouldBeTrue()
    {
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, new FakeWeekStatusHost());

        viewModel.InstallEnabled.Should().BeFalse();
        viewModel.UninstallEnabled.Should().BeTrue("already installed, so Remove is offered");
    }

    [Test]
    public void Install_WhenInstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var statusHost = new FakeWeekStatusHost();
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = false }, statusHost);

        viewModel.Install();

        viewModel.UninstallEnabled.Should().BeTrue("the install succeeded");
        statusHost.LastKind.Should().Be(WeekStatusKind.Success);
    }

    [Test]
    public void Uninstall_WhenUninstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var statusHost = new FakeWeekStatusHost();
        var viewModel = new MonitorSetupViewModel(
            new InMemoryInstaller { IsInstalled = true }, statusHost);

        viewModel.Uninstall();

        viewModel.InstallEnabled.Should().BeTrue("the autostart was removed");
        statusHost.LastKind.Should().Be(WeekStatusKind.Success);
    }
}
