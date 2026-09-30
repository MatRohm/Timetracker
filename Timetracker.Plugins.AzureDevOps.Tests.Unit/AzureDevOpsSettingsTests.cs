using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

[TestFixture]
public sealed class AzureDevOpsSettingsTests
{
    [Test]
    public void Current_WhenNothingIsStored_ShouldBeEmptyAndUnusable()
    {
        var settings = new AzureDevOpsSettings(new InMemoryOptionsStore());

        var config = settings.Current();

        config.Url.Should().BeEmpty();
        config.Project.Should().BeEmpty();
        config.Pat.Should().BeEmpty();
        config.IsUsable.Should().BeFalse();
    }

    [Test]
    public void Current_WhenOptionsAreStored_ShouldReturnThem()
    {
        var store = new InMemoryOptionsStore();
        store.Values[AzureDevOpsSettings.UrlKey] = "https://dev.azure.com/my-org";
        store.Values[AzureDevOpsSettings.ProjectKey] = "MyProject";
        store.Values[AzureDevOpsSettings.PatKey] = "secret-token";
        var settings = new AzureDevOpsSettings(store);

        var config = settings.Current();

        config.Url.Should().Be("https://dev.azure.com/my-org");
        config.Project.Should().Be("MyProject");
        config.Pat.Should().Be("secret-token");
        config.IsUsable.Should().BeTrue();
    }

    [Test]
    public void Current_WhenAnOptionChangesAfterStart_ShouldReturnTheNewValue()
    {
        var store = new InMemoryOptionsStore();
        var settings = new AzureDevOpsSettings(store);

        store.Values[AzureDevOpsSettings.ProjectKey] = "Later";

        settings.Current().Project.Should().Be("Later");
    }
}
