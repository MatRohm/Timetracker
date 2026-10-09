using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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

    [AvaloniaTest]
    public void OptionsTabView_WhenAnOptionHasAHint_ShouldShowTheInfoIconAndTooltip()
    {
        var view = CreateView(out _, hint: "Some hint");
        var window = new Window { Content = view };
        window.Show();

        var icons = view.GetLogicalDescendants().OfType<Viewbox>().ToList();
        icons.Should().ContainSingle("only the hinted option shows an info icon");
        icons[0].Child.Should().BeOfType<Avalonia.Controls.Shapes.Path>(
            "the icon is a drawn (i), not text");
        ToolTip.GetTip(icons[0]).Should().Be("Some hint");

        var hintedLabel = view.GetLogicalDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "Organization URL");
        hintedLabel.Width.Should().Be(200, "the label keeps a fixed column width so the icons align");
        var labelCell = (StackPanel)hintedLabel.Parent!;
        labelCell.Children.Should().HaveCount(2, "the cell holds the label and the icon");
    }

    [AvaloniaTest]
    public void OptionsTabView_WhenNoOptionHasAHint_ShouldRenderLabelsWithoutIcons()
    {
        var view = CreateView(out _);
        var window = new Window { Content = view };
        window.Show();

        view.GetLogicalDescendants().OfType<Viewbox>().Should().BeEmpty(
            "an option without a hint renders no icon");
        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.Should().Contain(["Tracking file", "Organization URL", "Personal access token"]);

        var labelCells = view.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => t.Text is "Tracking file" or "Organization URL" or "Personal access token");
        labelCells.Should().OnlyContain(t => t.Width == 200);
        var grid = view.GetLogicalDescendants().OfType<Grid>()
            .First(g => g.Children.OfType<TextBlock>().Any(t => t.Text == "Organization URL"));
        grid.ColumnDefinitions.Select(c => c.Width).Should().Equal(
            [GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto],
            "the label cell keeps the Auto,*,Auto layout");
    }

    [AvaloniaTest]
    public void OptionsTabView_WhenAContributionMatchesASection_ShouldRenderItBelowTheOptions()
    {
        var view = CreateView(out _, contributions:
        [
            ("Azure DevOps", new Button { Content = "Contributed" }),
        ]);
        var window = new Window { Content = view };
        window.Show();

        var button = view.GetLogicalDescendants().OfType<Button>()
            .Single(b => (b.Content as string) == "Contributed");

        // The contribution sits inside the "Azure DevOps" section, after its options.
        var section = button.GetLogicalAncestors().OfType<StackPanel>()
            .Single(p => p.Children.OfType<TextBlock>().Any(t => t.Text == "Azure DevOps"));
        section.Children.Should().Contain(button, "the contribution joins the matching section");
    }

    [AvaloniaTest]
    public void OptionsTabView_WhenAContributionMatchesNoSection_ShouldRenderItsOwnSection()
    {
        var view = CreateView(out _, contributions:
        [
            ("Activity monitor", new Button { Content = "Install" }),
        ]);
        var window = new Window { Content = view };
        window.Show();

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.Should().Contain("Activity monitor", "an unmatched section still gets a heading");
        view.GetLogicalDescendants().OfType<Button>()
            .Should().ContainSingle(b => (b.Content as string) == "Install");
    }

    [AvaloniaTest]
    public void OptionsTabView_WhenSeveralOptionsHaveHints_ShouldAlignTheirIcons()
    {
        var viewModel = new OptionsViewModel(
            new EmptyStore(),
            new EmptyStore(),
            [
                new Contributor("General",
                    new OptionDefinition("General.Short", "Short", OptionKind.Text, "v", HintText: "h"),
                    new OptionDefinition("General.AMuchLongerLabel", "A much longer label", OptionKind.Text, "v", HintText: "h")),
            ],
            new RecordingExplorer());
        var view = new OptionsTabView(viewModel, []);
        var window = new Window { Content = view, Width = 900, Height = 500 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var icons = view.GetLogicalDescendants().OfType<Viewbox>().ToList();
        icons.Should().HaveCount(2, "both options carry a hint");

        var xs = icons.Select(i => i.TranslatePoint(default, view)!.Value.X).ToList();
        xs[1].Should().Be(xs[0], "every hint icon sits at the same x-position");
    }

    private static OptionsTabView CreateView(
        out RecordingExplorer explorer,
        string? hint = null,
        IReadOnlyList<(string Section, Control Control)>? contributions = null)
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
                        "https://dev.azure.com/my-org", HintText: hint ?? ""),
                    new OptionDefinition("AzureDevOps.Pat", "Personal access token", OptionKind.Secret, "token")),
            ],
            explorer);
        var result = new OptionsTabView(viewModel, contributions ?? []);
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
