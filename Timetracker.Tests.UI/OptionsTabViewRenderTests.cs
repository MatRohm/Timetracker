using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using NUnit.Framework;
using Timetracker.App.Interfaces;
using Timetracker.App.ViewModels;
using Timetracker.App.Views;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Tests.UI;

/// <summary>
/// UI smoke tests for the options tab: build the real view on a headless Avalonia
/// instance and assert the section headings, the masked token and the explorer
/// buttons of the path options.
/// </summary>
[TestFixture]
public sealed class OptionsTabViewRenderTests
{
    [AvaloniaTest]
    public void OptionsTabView_WhenRendered_ShouldShowEverySectionWithItsOptions()
    {
        var view = CreateView(out _);
        var window = new Window { Content = view };
        window.Show();

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.Should().Contain(["General", "Tracking file", "Azure DevOps", "Personal access token"]);

        var boxes = view.GetLogicalDescendants().OfType<TextBox>().ToList();
        boxes.Select(b => b.Text).Should().Equal("C:/data/timetracker.json", "https://dev.azure.com/my-org", "token");
        boxes[0].IsReadOnly.Should().BeTrue();
        boxes[2].PasswordChar.Should().NotBe(default(char), "the token is masked");
    }

    [AvaloniaTest]
    public void OptionsTabView_WhenAPathsExplorerButtonIsClicked_ShouldShowThePath()
    {
        var view = CreateView(out var explorer);
        var window = new Window { Content = view };
        window.Show();

        var explorerButtons = view.GetLogicalDescendants().OfType<Button>()
            .Where(b => b.Name == "ShowInExplorerButton")
            .ToList();
        explorerButtons.Should().ContainSingle("only the tracking file is a path");
        explorerButtons[0].Content.Should().BeOfType<Avalonia.Controls.Shapes.Path>(
            "the button shows a folder icon, not text");
        explorerButtons[0].Command!.Execute(null);

        explorer.Shown.Should().Equal("C:/data/timetracker.json");
    }

    private static OptionsTabView CreateView(out RecordingExplorer explorer)
    {
        explorer = new RecordingExplorer();
        var viewModel = new OptionsViewModel(
            new EmptyStore(),
            new EmptyStore(),
            [
                new Contributor("General",
                    new OptionDefinition("General.TrackingFile", "Tracking file", OptionKind.Path,
                        "C:/data/timetracker.json", IsReadOnly: true)),
                new Contributor("Azure DevOps",
                    new OptionDefinition("AzureDevOps.Url", "Organization URL", OptionKind.Text,
                        "https://dev.azure.com/my-org"),
                    new OptionDefinition("AzureDevOps.Pat", "Personal access token", OptionKind.Secret, "token")),
            ],
            explorer);
        var result = new OptionsTabView(viewModel);
        return result;
    }

    private sealed class Contributor(string section, params OptionDefinition[] options) : IOptionDefinitionQuery
    {
        public string Section => section;

        public IReadOnlyList<OptionDefinition> Options => options;
    }

    private sealed class EmptyStore : IOptionQuery, IOptionCommand
    {
        public string? GetValue(string key) => null;

        public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingExplorer : IFileExplorer
    {
        public List<string> Shown { get; } = [];

        public void Show(string path) => Shown.Add(path);
    }
}
