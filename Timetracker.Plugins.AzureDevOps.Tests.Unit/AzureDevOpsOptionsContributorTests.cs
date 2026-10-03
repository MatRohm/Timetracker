using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

[TestFixture]
public sealed class AzureDevOpsOptionsContributorTests
{
    [Test]
    public void Options_WhenCreated_ShouldOfferUrlProjectAndAMaskedToken()
    {
        var contributor = new AzureDevOpsOptionsContributor();

        var options = contributor.Options;

        options.Select(o => (o.Key, o.Kind)).Should().Equal(
            (AzureDevOpsSettings.UrlKey, OptionKind.Text),
            (AzureDevOpsSettings.ProjectKey, OptionKind.Text),
            (AzureDevOpsSettings.PatKey, OptionKind.Secret),
            (AzureDevOpsSettings.BookingElementFieldKey, OptionKind.Text));
        options.Should().OnlyContain(o => !o.IsReadOnly && o.DefaultValue.Length == 0);
        contributor.Section.Should().Be("Azure DevOps");
    }

    [Test]
    public void Options_WhenCreated_ShouldCarryAHintForEveryOptionExceptProject()
    {
        var contributor = new AzureDevOpsOptionsContributor();

        var options = contributor.Options;

        options.Should().Contain(o => o.Key == AzureDevOpsSettings.UrlKey
            && o.HintText == "e.g. https://dev.azure.com/your-org");
        options.Should().Contain(o => o.Key == AzureDevOpsSettings.PatKey
            && o.HintText == "Azure DevOps personal access token; used as Basic password, user name stays empty");
        options.Should().Contain(o => o.Key == AzureDevOpsSettings.BookingElementFieldKey
            && o.HintText == "Reference name of the custom field holding the booking element");
        options.Should().ContainSingle(o => o.Key == AzureDevOpsSettings.ProjectKey && o.HintText.Length == 0);
    }
}
