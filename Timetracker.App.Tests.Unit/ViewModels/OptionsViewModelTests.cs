using AwesomeAssertions;
using FakeItEasy;
using NUnit.Framework;
using Timetracker.App.Interfaces;
using Timetracker.App.ViewModels;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.App.Tests.Unit.ViewModels;

[TestFixture]
public sealed class OptionsViewModelTests
{
    private static readonly OptionDefinition Url = new("AzureDevOps.Url", "Organization URL", OptionKind.Text, "https://default");
    private static readonly OptionDefinition Pat = new("AzureDevOps.Pat", "Personal access token", OptionKind.Secret);
    private static readonly OptionDefinition LogFolder = new("General.LogFolder", "Log folder", OptionKind.Path, "C:/logs", IsReadOnly: true);

    [Test]
    public void Sections_WhenContributorsAreRegistered_ShouldListOneSectionPerContributorInOrder()
    {
        var viewModel = Create(EmptyStore(), out _,
            Contributor("General", LogFolder), Contributor("Azure DevOps", Url, Pat));

        viewModel.Sections.Select(s => s.Title).Should().Equal("General", "Azure DevOps");
        viewModel.Sections[1].Rows.Select(r => r.Label).Should().Equal("Organization URL", "Personal access token");
    }

    [Test]
    public void Sections_WhenTheStoreHoldsAValue_ShouldShowItInsteadOfTheDefault()
    {
        var store = EmptyStore();
        A.CallTo(() => store.GetValue(Url.Key)).Returns("https://dev.azure.com/my-org");

        var viewModel = Create(store, out _, Contributor("Azure DevOps", Url));

        viewModel.Sections[0].Rows[0].Value.Should().Be("https://dev.azure.com/my-org");
    }

    [Test]
    public void Sections_WhenTheStoreHoldsNoValue_ShouldShowTheDefault()
    {
        var viewModel = Create(EmptyStore(), out _, Contributor("Azure DevOps", Url));

        viewModel.Sections[0].Rows[0].Value.Should().Be("https://default");
        viewModel.HasChanges.Should().BeFalse();
    }

    [Test]
    public void HasChanges_WhenAnOptionIsEdited_ShouldBeTrueAndEnableSave()
    {
        var viewModel = Create(EmptyStore(), out _, Contributor("Azure DevOps", Url));

        viewModel.Sections[0].Rows[0].Value = "https://dev.azure.com/other";

        viewModel.HasChanges.Should().BeTrue();
        viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public async Task SaveAsync_WhenOptionsWereEdited_ShouldStoreOnlyTheEditedOnes()
    {
        var store = EmptyStore();
        var viewModel = Create(store, out _, Contributor("Azure DevOps", Url, Pat));
        viewModel.Sections[0].Rows[1].Value = "new-token";

        await viewModel.SaveAsync();

        A.CallTo(() => store.SetValueAsync(Pat.Key, "new-token", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => store.SetValueAsync(Url.Key, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
        viewModel.HasChanges.Should().BeFalse();
        viewModel.StatusText.Should().Be("Options saved.");
    }

    [Test]
    public async Task SaveAsync_WhenTheFileCannotBeWritten_ShouldReportItAndKeepTheChange()
    {
        var store = EmptyStore();
        A.CallTo(() => store.SetValueAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("disk full"));
        var viewModel = Create(store, out _, Contributor("Azure DevOps", Url));
        viewModel.Sections[0].Rows[0].Value = "https://dev.azure.com/other";

        await viewModel.SaveAsync();

        viewModel.StatusText.Should().Contain("disk full");
        viewModel.HasChanges.Should().BeTrue("the edit was not saved");
    }

    [Test]
    public void Sections_WhenAPathRowAsksForTheExplorer_ShouldShowItsPath()
    {
        var viewModel = Create(EmptyStore(), out var explorer, Contributor("General", LogFolder));
        var row = viewModel.Sections[0].Rows[0];

        row.ShowInExplorerCommand.Execute(null);

        A.CallTo(() => explorer.Show("C:/logs")).MustHaveHappenedOnceExactly();
    }

    /// <summary>A store without values; FakeItEasy would return "" instead of null.</summary>
    private static IOptionsStore EmptyStore()
    {
        var store = A.Fake<IOptionsStore>();
        A.CallTo(() => store.GetValue(A<string>._)).Returns(null);
        return store;
    }

    private static OptionsViewModel Create(
        IOptionsStore store, out IFileExplorer explorer, params IOptionsContributor[] contributors)
    {
        explorer = A.Fake<IFileExplorer>();
        var result = new OptionsViewModel(store, contributors, explorer);
        return result;
    }

    private static IOptionsContributor Contributor(string section, params OptionDefinition[] options)
    {
        var contributor = A.Fake<IOptionsContributor>();
        A.CallTo(() => contributor.Section).Returns(section);
        A.CallTo(() => contributor.Options).Returns(options);
        return contributor;
    }
}
