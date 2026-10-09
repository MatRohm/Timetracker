using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class PathMigratorTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    [Test]
    public void MoveFile_WhenOnlyOldExists_ShouldMoveIt()
    {
        var dir = TempDir();
        var oldPath = Path.Combine(dir, "old.json");
        var newPath = Path.Combine(dir, "sub", "new.json");
        File.WriteAllText(oldPath, "data");

        PathMigrator.MoveFile(oldPath, newPath, Logger);

        File.Exists(oldPath).Should().BeFalse("the legacy file was moved");
        File.ReadAllText(newPath).Should().Be("data");
    }

    [Test]
    public void MoveFile_WhenBothExist_ShouldLeaveBoth()
    {
        var dir = TempDir();
        var oldPath = Path.Combine(dir, "old.json");
        var newPath = Path.Combine(dir, "sub", "new.json");
        File.WriteAllText(oldPath, "old");
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.WriteAllText(newPath, "new");

        PathMigrator.MoveFile(oldPath, newPath, Logger);

        File.ReadAllText(oldPath).Should().Be("old");
        File.ReadAllText(newPath).Should().Be("new");
    }

    [Test]
    public void MoveFile_WhenPathsAreEqual_ShouldDoNothing()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "x.json");
        File.WriteAllText(path, "data");

        PathMigrator.MoveFile(path, path, Logger);

        File.ReadAllText(path).Should().Be("data");
    }

    [Test]
    public void MoveFile_WhenOldIsMissing_ShouldDoNothing()
    {
        var dir = TempDir();
        var oldPath = Path.Combine(dir, "missing.json");
        var newPath = Path.Combine(dir, "sub", "new.json");

        PathMigrator.MoveFile(oldPath, newPath, Logger);

        File.Exists(newPath).Should().BeFalse();
    }

    [Test]
    public void MoveFolder_WhenOldExists_ShouldMoveItsFiles()
    {
        var dir = TempDir();
        var oldFolder = Path.Combine(dir, "oldlogs");
        var newFolder = Path.Combine(dir, "sub", "logs");
        Directory.CreateDirectory(oldFolder);
        File.WriteAllText(Path.Combine(oldFolder, "a.log"), "a");

        PathMigrator.MoveFolder(oldFolder, newFolder, Logger);

        File.Exists(Path.Combine(newFolder, "a.log")).Should().BeTrue("the log file was moved");
        File.Exists(Path.Combine(oldFolder, "a.log")).Should().BeFalse();
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opencode", "tt-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
