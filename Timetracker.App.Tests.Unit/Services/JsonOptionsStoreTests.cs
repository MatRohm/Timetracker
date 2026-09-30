using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class JsonOptionsStoreTests
{
    private string _dir = "";

    [SetUp]
    public void CreateDirectory()
    {
        _dir = Path.Combine(Path.GetTempPath(), "opencode", "tt-options-tests", Guid.NewGuid().ToString("N"));
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
            // Best effort: a leftover temp folder does not affect other tests.
        }
    }

    [Test]
    public async Task SetValueAsync_WhenAValueIsSet_ShouldBeReadByANewStoreOnTheSameFile()
    {
        var path = OptionsPath();
        await new JsonOptionsStore(path).SetValueAsync("AzureDevOps.Url", "https://dev.azure.com/my-org");

        var reloaded = new JsonOptionsStore(path);

        reloaded.GetValue("AzureDevOps.Url").Should().Be("https://dev.azure.com/my-org");
    }

    [Test]
    public async Task SetValueAsync_WhenTwoComponentsStoreOptions_ShouldKeepBothKeys()
    {
        var path = OptionsPath();
        var store = new JsonOptionsStore(path);

        await store.SetValueAsync("AzureDevOps.Project", "MyProject");
        await store.SetValueAsync("ActivityMonitor.Threshold", "60");

        var reloaded = new JsonOptionsStore(path);
        reloaded.GetValue("AzureDevOps.Project").Should().Be("MyProject");
        reloaded.GetValue("ActivityMonitor.Threshold").Should().Be("60");
    }

    [Test]
    public async Task SetValueAsync_WhenTheValueIsNull_ShouldRemoveTheOption()
    {
        var path = OptionsPath();
        var store = new JsonOptionsStore(path);
        await store.SetValueAsync("AzureDevOps.Pat", "secret");

        await store.SetValueAsync("AzureDevOps.Pat", null);

        store.GetValue("AzureDevOps.Pat").Should().BeNull();
        new JsonOptionsStore(path).GetValue("AzureDevOps.Pat").Should().BeNull();
    }

    [Test]
    public void GetValue_WhenTheFileIsMissing_ShouldReturnNull()
    {
        var store = new JsonOptionsStore(OptionsPath());

        store.GetValue("AzureDevOps.Url").Should().BeNull();
    }

    [Test]
    public void GetValue_WhenTheFileIsBroken_ShouldReturnNullAndLogTheError()
    {
        var path = OptionsPath();
        File.WriteAllText(path, "{ not valid json");
        var logger = new FakeLogger<JsonOptionsStore>();

        var store = new JsonOptionsStore(path, logger);

        store.GetValue("AzureDevOps.Url").Should().BeNull();
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
        File.ReadAllText(path).Should().Be("{ not valid json", "loading never rewrites the file");
    }

    private string OptionsPath() => Path.Combine(_dir, "options.json");
}
