using AwesomeAssertions;
using FakeItEasy;
using NUnit.Framework;
using Timetracker.App.Interfaces;
using Timetracker.App.ViewModels;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Tests.Unit.ViewModels;

[TestFixture]
public sealed class OptionRowViewModelTests
{
    [Test]
    public void Value_WhenTheOptionIsReadOnly_ShouldIgnoreEditsAndTheStoredValue()
    {
        var definition = new OptionDefinition("General.TrackingFile", "Tracking file", OptionKind.Path, "C:/t.json", IsReadOnly: true);
        var row = new OptionRowViewModel(definition, "stale stored value", A.Fake<IFileExplorer>());

        row.Value = "C:/other.json";

        row.Value.Should().Be("C:/t.json");
        row.IsChanged.Should().BeFalse();
    }

    [Test]
    public void AcceptValue_WhenTheValueWasEdited_ShouldClearTheChange()
    {
        var definition = new OptionDefinition("AzureDevOps.Project", "Project", OptionKind.Text);
        var row = new OptionRowViewModel(definition, null, A.Fake<IFileExplorer>());
        row.Value = "MyProject";

        row.AcceptValue();

        row.IsChanged.Should().BeFalse();
        row.Value.Should().Be("MyProject");
    }

    [Test]
    public void ShowInExplorerCommand_WhenTheOptionIsNoPath_ShouldBeDisabled()
    {
        var definition = new OptionDefinition("AzureDevOps.Pat", "Personal access token", OptionKind.Secret);
        var row = new OptionRowViewModel(definition, null, A.Fake<IFileExplorer>());

        row.ShowInExplorerCommand.CanExecute(null).Should().BeFalse();
        row.IsSecret.Should().BeTrue();
    }
}
