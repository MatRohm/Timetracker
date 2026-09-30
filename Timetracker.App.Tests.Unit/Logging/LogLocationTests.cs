using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts.Logging;

namespace Timetracker.App.Tests.Unit.Logging;

[TestFixture]
public sealed class LogLocationTests
{
    private static readonly string LocalAppData = Path.Combine(Path.GetTempPath(), "local-app-data");
    private static readonly string Home = Path.Combine(Path.GetTempPath(), "home");

    [Test]
    public void Resolve_WhenOnWindows_ShouldUseTheLocalApplicationDataFolder()
    {
        var folder = LogLocation.Resolve(isWindows: true, _ => null, LocalAppData, Home);

        folder.Should().Be(Path.Combine(LocalAppData, "Timetracker", "logs"));
    }

    [Test]
    public void Resolve_WhenOnLinuxWithXdgStateHome_ShouldUseIt()
    {
        var stateHome = Path.Combine(Path.GetTempPath(), "state-home");

        var folder = LogLocation.Resolve(
            isWindows: false, name => name == "XDG_STATE_HOME" ? stateHome : null, LocalAppData, Home);

        folder.Should().Be(Path.Combine(stateHome, "timetracker", "logs"));
    }

    [Test]
    public void Resolve_WhenOnLinuxWithoutXdgStateHome_ShouldUseTheDefaultStateFolder()
    {
        var folder = LogLocation.Resolve(isWindows: false, _ => null, LocalAppData, Home);

        folder.Should().Be(Path.Combine(Home, ".local", "state", "timetracker", "logs"));
    }

    [Test]
    public void Resolve_WhenXdgStateHomeIsRelative_ShouldIgnoreIt()
    {
        var folder = LogLocation.Resolve(isWindows: false, _ => "relative/state", LocalAppData, Home);

        folder.Should().Be(Path.Combine(Home, ".local", "state", "timetracker", "logs"),
            "the XDG specification only allows absolute paths");
    }
}
