using Avalonia.Media;
using Avalonia.Styling;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Services;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.App.Tests.Unit.Services;

[TestFixture]
public sealed class ThemeSwitcherTests
{
    [Test]
    public void CreateTheme_WhenCalled_ShouldUseTheDraculaPaletteForDark()
    {
        var theme = ThemeSwitcher.CreateTheme();

        var dark = theme.Palettes[ThemeVariant.Dark];

        dark.RegionColor.Should().Be(Color.Parse("#FF282A36"), "Dracula's background");
        dark.Accent.Should().Be(Color.Parse("#FFBD93F9"), "Dracula's purple");
        dark.BaseHigh.Should().Be(Color.Parse("#FFF8F8F2"), "Dracula's foreground");
    }

    [Test]
    public void CreateTheme_WhenCalled_ShouldUseTheAlucardPaletteForLight()
    {
        var theme = ThemeSwitcher.CreateTheme();

        var light = theme.Palettes[ThemeVariant.Light];

        light.RegionColor.Should().Be(Color.Parse("#FFFFFBEB"), "Alucard's background");
        light.Accent.Should().Be(Color.Parse("#FF644AC9"), "Alucard's purple");
        light.BaseHigh.Should().Be(Color.Parse("#FF1F1F1F"), "Alucard's foreground");
        light.AltMediumLow.Should().Be(Color.Parse("#66FFFFFF"),
            "Fluent uses AltMediumLow as the default text-box backdrop, which must stay bright in light mode");
    }

    [Test]
    public void Apply_WhenTheThemeIsDark_ShouldRecolorTheSemanticBrushesForDark()
    {
        try
        {
            new ThemeSwitcher().Apply(AppearanceOptions.Dark);

            ((ISolidColorBrush)ViewBrushes.Today).Color.Should().Be(Color.Parse("#FF44475A"));
            ((ISolidColorBrush)ViewBrushes.Success).Color.Should().Be(Color.Parse("#FF50FA7B"));
        }
        finally
        {
            ViewBrushes.Apply(dark: false);
        }
    }

    [Test]
    public void Apply_WhenTheThemeIsUnknown_ShouldUseTheLightBrushes()
    {
        try
        {
            ViewBrushes.Apply(dark: true);

            new ThemeSwitcher().Apply("Unknown");

            ((ISolidColorBrush)ViewBrushes.Today).Color.Should().Be(Color.Parse("#FFF2E6C9"));
            ((ISolidColorBrush)ViewBrushes.Success).Color.Should().Be(Color.Parse("#FF14710A"));
        }
        finally
        {
            ViewBrushes.Apply(dark: false);
        }
    }
}
