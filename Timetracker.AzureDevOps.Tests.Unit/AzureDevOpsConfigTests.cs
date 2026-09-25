using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.AzureDevOps.Tests.Unit;

[TestFixture]
public sealed class AzureDevOpsConfigTests
{
    [Test]
    public void Load_WhenTheJsonFileExists_ShouldReadItsValues()
    {
        var path = AzureDevOpsTestHelpers.TempPath("config-ok.json");
        File.WriteAllText(path, """
            {
              "url": "https://dev.azure.com/my-org",
              "project": "MyProject",
              "pat": "secret-token"
            }
            """);

        var config = AzureDevOpsConfig.Load(path);

        config.Url.Should().Be("https://dev.azure.com/my-org");
        config.Project.Should().Be("MyProject");
        config.Pat.Should().Be("secret-token");
        config.IsUsable.Should().BeTrue();
    }

    [Test]
    public void Load_WhenTheJsonFileIsMissing_ShouldYieldAnEmptyUnusableConfig()
    {
        var config = AzureDevOpsConfig.Load(AzureDevOpsTestHelpers.TempPath("does-not-exist.json"));

        config.Url.Should().BeEmpty();
        config.IsUsable.Should().BeFalse();
    }

    [Test]
    public void Load_WhenTheJsonFileIsBroken_ShouldYieldAnEmptyConfigInsteadOfCrashing()
    {
        var path = AzureDevOpsTestHelpers.TempPath("config-broken.json");
        File.WriteAllText(path, "{ not valid json ");

        var config = AzureDevOpsConfig.Load(path);

        config.IsUsable.Should().BeFalse();
    }

    [Test]
    public void IsUsable_WhenThePatIsMissing_ShouldBeFalse()
    {
        var config = new AzureDevOpsConfig
        {
            Url = "https://dev.azure.com/my-org",
            Project = "MyProject",
            Pat = "  ",
        };

        config.IsUsable.Should().BeFalse();
    }
}
