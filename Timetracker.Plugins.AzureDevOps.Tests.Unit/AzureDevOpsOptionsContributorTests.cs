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
}
