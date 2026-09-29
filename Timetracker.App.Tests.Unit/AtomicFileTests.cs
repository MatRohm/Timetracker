using NUnit.Framework;
using AwesomeAssertions;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class AtomicFileTests
{
    private string _dir = "";

    [SetUp]
    public void CreateDirectory()
    {
        _dir = Path.Combine(Path.GetTempPath(), "opencode", "tt-atomic-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void DeleteDirectory()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder must not fail the test run.
        }
    }

    [Test]
    public async Task WriteAllTextAsync_WhenTheFileDoesNotExist_ShouldCreateItWithoutAByteOrderMark()
    {
        var path = Path.Combine(_dir, "tracker.json");

        await AtomicFile.WriteAllTextAsync(path, "{\"version\":2}");

        (await File.ReadAllTextAsync(path)).Should().Be("{\"version\":2}");
        (await File.ReadAllBytesAsync(path))[0].Should().Be((byte)'{', "the file must not start with a UTF-8 BOM");
        LeftoverTempFiles().Should().BeEmpty();
    }

    [Test]
    public async Task WriteAllTextAsync_WhenTheFileExists_ShouldReplaceItsContent()
    {
        var path = Path.Combine(_dir, "tracker.json");
        await File.WriteAllTextAsync(path, "old content that is longer than the new one");

        await AtomicFile.WriteAllTextAsync(path, "new");

        (await File.ReadAllTextAsync(path)).Should().Be("new");
        LeftoverTempFiles().Should().BeEmpty();
    }

    [Test]
    public async Task WriteAllTextAsync_WhenTheTargetCannotBeReplaced_ShouldThrowAndLeaveNoTempFile()
    {
        // A directory in place of the file makes every rename fail on every OS.
        var path = Path.Combine(_dir, "tracker.json");
        Directory.CreateDirectory(path);

        var act = () => AtomicFile.WriteAllTextAsync(path, "new");

        await act.Should().ThrowAsync<Exception>();
        Directory.Exists(path).Should().BeTrue();
        LeftoverTempFiles().Should().BeEmpty();
    }

    [Test]
    [Platform("Win", Reason = "Only Windows blocks replacing a file another handle has open.")]
    public async Task WriteAllTextAsync_WhenTheFileStaysLocked_ShouldThrowAndKeepTheOriginalContent()
    {
        var path = Path.Combine(_dir, "tracker.json");
        await File.WriteAllTextAsync(path, "original");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var act = () => AtomicFile.WriteAllTextAsync(path, "new");

            await act.Should().ThrowAsync<Exception>()
                .Where(e => e is IOException || e is UnauthorizedAccessException);
        }

        (await File.ReadAllTextAsync(path)).Should().Be("original");
        LeftoverTempFiles().Should().BeEmpty();
    }

    [Test]
    [Platform("Win", Reason = "Only Windows blocks replacing a file another handle has open.")]
    public async Task WriteAllTextAsync_WhenTheLockIsReleasedDuringTheRetries_ShouldReplaceTheFile()
    {
        var path = Path.Combine(_dir, "tracker.json");
        await File.WriteAllTextAsync(path, "original");
        var lockHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var write = AtomicFile.WriteAllTextAsync(path, "new");
        await Task.Delay(150);
        await lockHandle.DisposeAsync();
        await write;

        (await File.ReadAllTextAsync(path)).Should().Be("new");
        LeftoverTempFiles().Should().BeEmpty();
    }

    [Test]
    public void WriteAllText_WhenTheFileExists_ShouldReplaceItsContent()
    {
        var path = Path.Combine(_dir, "tracker.json");
        File.WriteAllText(path, "old content that is longer than the new one");

        AtomicFile.WriteAllText(path, "new");

        File.ReadAllText(path).Should().Be("new");
        LeftoverTempFiles().Should().BeEmpty();
    }

    [Test]
    [Platform("Win", Reason = "Only Windows blocks replacing a file another handle has open.")]
    public void WriteAllText_WhenTheFileStaysLocked_ShouldThrowAndKeepTheOriginalContent()
    {
        var path = Path.Combine(_dir, "tracker.json");
        File.WriteAllText(path, "original");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var act = () => AtomicFile.WriteAllText(path, "new");

            act.Should().Throw<Exception>()
                .Where(e => e is IOException || e is UnauthorizedAccessException);
        }

        File.ReadAllText(path).Should().Be("original");
        LeftoverTempFiles().Should().BeEmpty();
    }

    private string[] LeftoverTempFiles() => Directory.GetFiles(_dir, "*.tmp");
}
