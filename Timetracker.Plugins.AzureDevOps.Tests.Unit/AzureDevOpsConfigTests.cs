using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

[TestFixture]
public sealed class AzureDevOpsConfigTests
{
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
