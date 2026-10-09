using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Tests.Unit;

[TestFixture]
public sealed class TimetrackerPathsTests
{
    [Test]
    public void ResolveFolder_WhenGivenAFolder_ShouldReturnIt()
    {
        var folder = TimetrackerPaths.ResolveFolder("/custom/tt");

        folder.Should().Be("/custom/tt");
    }

    [Test]
    public void ResolveFolder_WhenGivenNullOrEmpty_ShouldReturnTheRootFolder()
    {
        TimetrackerPaths.ResolveFolder(null).Should().Be(TimetrackerPaths.RootFolder);
        TimetrackerPaths.ResolveFolder("").Should().Be(TimetrackerPaths.RootFolder);
        TimetrackerPaths.ResolveFolder("   ").Should().Be(TimetrackerPaths.RootFolder);
    }

    [Test]
    public void OptionsFile_WhenRead_ShouldBeTheFixedAnchorUnderTheRootFolder()
    {
        TimetrackerPaths.OptionsFile.Should().Be(
            Path.Combine(TimetrackerPaths.RootFolder, "timetracker-options.json"));
    }
}
