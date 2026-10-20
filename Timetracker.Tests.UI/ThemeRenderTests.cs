using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Interfaces;
using Timetracker.App.Services;
using Timetracker.App.ViewModels;
using Timetracker.App.Views;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Tests.UI;

/// <summary>
/// Proves the real headless application switches its Fluent variant and the shared
/// view brushes together, which is what a dark-mode switch must do at runtime.
/// </summary>
[TestFixture]
public sealed class ThemeRenderTests
{
    [AvaloniaTest]
    public void ThemeSwitcher_WhenDarkIsApplied_ShouldSwitchTheApplicationAndTheViewBrushes()
    {
        var switcher = new ThemeSwitcher();
        try
        {
            switcher.Apply(AppearanceOptions.Dark);
            Dispatcher.UIThread.RunJobs();

            Application.Current!.ActualThemeVariant.Should().Be(ThemeVariant.Dark);
            ((ISolidColorBrush)ViewBrushes.Today).Color.Should().Be(Color.Parse("#FF44475A"));

            var window = new Window { Content = new TextBlock { Text = "Theme" } };
            window.Show();
            window.Background.Should().BeOfType<SolidColorBrush>()
                .Which.Color.Should().Be(Color.Parse("#FF282A36"), "the window renders Dracula's background");
            window.Close();
        }
        finally
        {
            switcher.Apply(AppearanceOptions.Light);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTest]
    public void ThemeSwitcher_WhenDarkIsAppliedAfterTheWindowIsShown_ShouldRecolorTheLiveWindow()
    {
        var switcher = new ThemeSwitcher();
        try
        {
            switcher.Apply(AppearanceOptions.Light);
            var window = new Window { Content = new TextBlock { Text = "Theme" } };
            window.Show();
            window.Background.Should().BeOfType<SolidColorBrush>()
                .Which.Color.Should().Be(Color.Parse("#FFFFFBEB"), "the window starts light");

            switcher.Apply(AppearanceOptions.Dark);
            Dispatcher.UIThread.RunJobs();

            window.Background.Should().BeOfType<SolidColorBrush>()
                .Which.Color.Should().Be(Color.Parse("#FF282A36"), "the live window follows the switch");
            window.Close();
        }
        finally
        {
            switcher.Apply(AppearanceOptions.Light);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTest]
    public void OptionsTab_WhenTheThemeChoiceIsPicked_ShouldSwitchTheLiveWindow()
    {
        var switcher = new ThemeSwitcher();
        try
        {
            switcher.Apply(AppearanceOptions.Light);
            var theme = new OptionDefinition(
                AppearanceOptions.ThemeKey,
                "Theme",
                OptionKind.Choice,
                AppearanceOptions.Light)
            {
                Choices =
                [
                    new OptionChoice(AppearanceOptions.Light, "Light"),
                    new OptionChoice(AppearanceOptions.Dark, "Dark"),
                ],
            };
            var store = new EmptyStore();
            var viewModel = new OptionsViewModel(
                store, store, [new ThemeContributor(theme)], new NoExplorer(), switcher);
            var view = new OptionsTabView(viewModel, []);
            var window = new Window { Content = view };
            window.Show();

            var combo = view.GetLogicalDescendants().OfType<ComboBox>().Single();
            combo.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            viewModel.Sections[0].Rows[0].Value.Should().Be(AppearanceOptions.Dark);
            combo.SelectedIndex.Should().Be(1);
            combo.SelectedItem.Should().Be("Dark", "the dropdown keeps showing the picked choice");
            combo.SelectionBoxItem.Should().Be("Dark", "the closed dropdown displays the picked choice");
            window.Background.Should().BeOfType<SolidColorBrush>()
                .Which.Color.Should().Be(Color.Parse("#FF282A36"), "picking Dark switches the live window");
            window.Close();
        }
        finally
        {
            switcher.Apply(AppearanceOptions.Light);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class ThemeContributor(OptionDefinition theme) : IOptionDefinitionQuery
    {
        public string Section => "General";

        public IReadOnlyList<OptionDefinition> Options => [theme];
    }

    private sealed class EmptyStore : IOptionQuery, IOptionCommand
    {
        public string? GetValue(string key) => null;

        public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoExplorer : IFileExplorer
    {
        public void Show(string path)
        {
        }
    }
}
