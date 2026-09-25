using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class ActivityMonitorInstallerTests
{
    [Test]
    public void Install_WhenInstallCompletes_ShouldReportInstalled()
    {
        // Exercises the platform installer contract through the in-memory fake.
        var installer = new InMemoryInstaller { IsInstalled = false };

        installer.Install();

        installer.IsInstalled.Should().BeTrue();
        installer.MonitorExePath.Should().NotBeNullOrWhiteSpace();
    }
}
