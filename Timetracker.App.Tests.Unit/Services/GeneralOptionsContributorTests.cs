using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Services;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class GeneralOptionsContributorTests
{
    [Test]
    public void Options_WhenCreated_ShouldShowConfigFolderThenTheThreeReadOnlyPaths()
    {
        var contributor = new GeneralOptionsContributor("C:/data/timetracker.json", "C:/data/options.json", "C:/logs");

        var options = contributor.Options;

        options.Select(o => (o.Label, o.DefaultValue)).Should().Equal(
            ("Config folder", ""),
            ("Tracking file", "C:/data/timetracker.json"),
            ("Options file", "C:/data/options.json"),
            ("Log folder", "C:/logs"));
        options[0].Kind.Should().Be(OptionKind.Path);
        options[0].IsReadOnly.Should().BeFalse("the config folder is editable");
        options.Skip(1).Should().OnlyContain(o => o.Kind == OptionKind.Path && o.IsReadOnly);
    }

    [Test]
    public void Options_WhenCreated_ShouldExplainEveryPath()
    {
        var contributor = new GeneralOptionsContributor("C:/data/timetracker.json", "C:/data/options.json", "C:/logs");

        var options = contributor.Options;

        options.Should().OnlyContain(o => o.HintText.Length > 0);
        options.Should().Contain(o => o.Key == "General.TrackingFile"
            && o.HintText == "The tracked sessions are stored in this JSON file.");
        options.Should().Contain(o => o.Key == "General.OptionsFile"
            && o.HintText == "The options are stored in this JSON file.");
        options.Should().Contain(o => o.Key == "General.LogFolder"
            && o.HintText == "Diagnostic log files are written to this folder.");
    }

    [Test]
    public void Section_WhenRead_ShouldBeGeneral()
    {
        var contributor = new GeneralOptionsContributor("a", "b", "c");

        contributor.Section.Should().Be("General");
    }
}
