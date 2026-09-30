using AwesomeAssertions;
using NUnit.Framework;
using static Timetracker.App.Services.FileExplorer;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class FileExplorerTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "tt-explorer");
    private static readonly string TrackingFile = Path.Combine(Folder, "timetracker.json");

    [Test]
    public void CommandFor_WhenTheFileExistsOnWindows_ShouldSelectItInExplorer()
    {
        var command = CommandFor(TrackingFile, Platform.Windows, p => p == TrackingFile, p => p == Folder);

        command.Should().Be(new Command("explorer.exe", $"/select,\"{TrackingFile}\""));
    }

    [Test]
    public void CommandFor_WhenTheFileExistsOnMacOS_ShouldRevealItInTheFinder()
    {
        var command = CommandFor(TrackingFile, Platform.MacOS, p => p == TrackingFile, p => p == Folder);

        command.Should().Be(new Command("open", $"-R \"{TrackingFile}\""));
    }

    [Test]
    public void CommandFor_WhenTheFileExistsOnLinux_ShouldOpenItsFolder()
    {
        var command = CommandFor(TrackingFile, Platform.Linux, p => p == TrackingFile, p => p == Folder);

        command.Should().Be(new Command("xdg-open", $"\"{Folder}\""));
    }

    [Test]
    public void CommandFor_WhenThePathIsAFolder_ShouldOpenTheFolder()
    {
        var command = CommandFor(Folder, Platform.Windows, _ => false, p => p == Folder);

        command.Should().Be(new Command("explorer.exe", $"\"{Folder}\""));
    }

    [Test]
    public void CommandFor_WhenThePathDoesNotExistYet_ShouldOpenTheNearestExistingFolder()
    {
        var missing = Path.Combine(Folder, "logs", "app.log");

        var command = CommandFor(missing, Platform.Linux, _ => false, p => p == Folder);

        command.Should().Be(new Command("xdg-open", $"\"{Folder}\""));
    }

    [Test]
    public void CommandFor_WhenThePathIsEmpty_ShouldReturnNull()
    {
        var command = CommandFor("  ", Platform.Windows, _ => true, _ => true);

        command.Should().BeNull();
    }
}
