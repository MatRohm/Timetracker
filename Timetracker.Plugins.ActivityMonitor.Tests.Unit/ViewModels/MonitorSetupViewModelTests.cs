using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.ActivityMonitor.ViewModels;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit.ViewModels;

[TestFixture]
public sealed class MonitorSetupViewModelTests
{
    [Test]
    public void InstallEnabled_WhenMonitorIsNotInstalled_ShouldBeTrue()
    {
        var viewModel = new MonitorSetupViewModel(new InMemoryInstaller { IsInstalled = false });

        viewModel.InstallEnabled.Should().BeTrue("not installed yet, so Install is offered");
        viewModel.UninstallEnabled.Should().BeFalse();
    }

    [Test]
    public void UninstallEnabled_WhenMonitorIsInstalled_ShouldBeTrue()
    {
        var viewModel = new MonitorSetupViewModel(new InMemoryInstaller { IsInstalled = true });

        viewModel.InstallEnabled.Should().BeFalse();
        viewModel.UninstallEnabled.Should().BeTrue("already installed, so Remove is offered");
    }

    [Test]
    public void Install_WhenInstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var viewModel = new MonitorSetupViewModel(new InMemoryInstaller { IsInstalled = false });

        viewModel.Install();

        viewModel.UninstallEnabled.Should().BeTrue("the install succeeded");
        viewModel.ResultIsError.Should().BeFalse("the result is reported as success");
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public void Install_WhenInstallFails_ShouldReportTheFailure()
    {
        var viewModel = new MonitorSetupViewModel(new FailingInstaller());

        viewModel.Install();

        viewModel.ResultIsError.Should().BeTrue("the result is reported as a failure");
        viewModel.ResultText.Should().NotBeEmpty();
    }

    [Test]
    public void Uninstall_WhenUninstallSucceeds_ShouldFlipButtonStateAndReportSuccess()
    {
        var viewModel = new MonitorSetupViewModel(new InMemoryInstaller { IsInstalled = true });

        viewModel.Uninstall();

        viewModel.InstallEnabled.Should().BeTrue("the autostart was removed");
        viewModel.ResultIsError.Should().BeFalse();
        viewModel.ResultText.Should().NotBeEmpty();
    }

    /// <summary>An installer whose <see cref="IActivityMonitorInstaller.Install"/> always fails.</summary>
    private sealed class FailingInstaller : IActivityMonitorInstaller
    {
        public string MonitorExePath => "Timetracker.Plugins.ActivityMonitor";

        public bool IsInstalled => false;

        public bool Install() => false;

        public bool Uninstall() => true;
    }
}
