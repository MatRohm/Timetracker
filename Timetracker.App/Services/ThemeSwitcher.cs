using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Timetracker.App.Interfaces;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.App.Services;

/// <summary>
/// Owns the app's Fluent color palettes and applies the user's Light/Dark setting.
/// Dark is based on Dracula; light is its Alucard counterpart, so both variants
/// share one designed scheme instead of the stock Fluent colors.
/// </summary>
public sealed class ThemeSwitcher : IThemeSwitcher
{
    /// <summary>
    /// Builds the Fluent theme with the app's own palettes. The palettes are
    /// configured once at startup; switching only changes the requested variant.
    /// </summary>
    public static FluentTheme CreateTheme()
    {
        var theme = new FluentTheme();
        theme.Palettes[ThemeVariant.Light] = CreateLightPalette();
        theme.Palettes[ThemeVariant.Dark] = CreateDarkPalette();
        return theme;
    }

    /// <summary>Switches the application variant and recolors the shared view brushes.</summary>
    public void Apply(string? theme)
    {
        var isDark = AppearanceOptions.IsDark(theme);
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        ViewBrushes.Apply(isDark);
    }

    /// <summary>Dracula: background #282A36, foreground #F8F8F2, accent #BD93F9.</summary>
    private static ColorPaletteResources CreateDarkPalette() => new()
    {
        Accent = Color.Parse("#FFBD93F9"),
        AltHigh = Color.Parse("#FF282A36"),
        AltLow = Color.Parse("#33000000"),
        AltMedium = Color.Parse("#99000000"),
        AltMediumHigh = Color.Parse("#CC000000"),
        AltMediumLow = Color.Parse("#66000000"),
        BaseHigh = Color.Parse("#FFF8F8F2"),
        BaseLow = Color.Parse("#33F8F8F2"),
        BaseMedium = Color.Parse("#99F8F8F2"),
        BaseMediumHigh = Color.Parse("#CCF8F8F2"),
        BaseMediumLow = Color.Parse("#66F8F8F2"),
        ChromeAltLow = Color.Parse("#FFF8F8F2"),
        ChromeBlackHigh = Color.Parse("#FF191A21"),
        ChromeBlackLow = Color.Parse("#33000000"),
        ChromeBlackMedium = Color.Parse("#CC000000"),
        ChromeBlackMediumLow = Color.Parse("#66000000"),
        ChromeDisabledHigh = Color.Parse("#FF44475A"),
        ChromeDisabledLow = Color.Parse("#FF6272A4"),
        ChromeGray = Color.Parse("#FF6272A4"),
        ChromeHigh = Color.Parse("#FF6272A4"),
        ChromeLow = Color.Parse("#FF21222C"),
        ChromeMedium = Color.Parse("#FF282A36"),
        ChromeMediumLow = Color.Parse("#FF2F3241"),
        ChromeWhite = Color.Parse("#FFF8F8F2"),
        ErrorText = Color.Parse("#FFFF5555"),
        ListLow = Color.Parse("#19F8F8F2"),
        ListMedium = Color.Parse("#33F8F8F2"),
        RegionColor = Color.Parse("#FF282A36"),
    };

    /// <summary>Alucard: background #FFFBEB, foreground #1F1F1F, accent #644AC9.</summary>
    private static ColorPaletteResources CreateLightPalette() => new()
    {
        Accent = Color.Parse("#FF644AC9"),
        AltHigh = Color.Parse("#FFFFFBEB"),
        AltLow = Color.Parse("#33FFFFFF"),
        AltMedium = Color.Parse("#99FFFFFF"),
        AltMediumHigh = Color.Parse("#CCFFFFFF"),
        AltMediumLow = Color.Parse("#66FFFFFF"),
        BaseHigh = Color.Parse("#FF1F1F1F"),
        BaseLow = Color.Parse("#33000000"),
        BaseMedium = Color.Parse("#99000000"),
        BaseMediumHigh = Color.Parse("#CC000000"),
        BaseMediumLow = Color.Parse("#66000000"),
        ChromeAltLow = Color.Parse("#FF1F1F1F"),
        ChromeBlackHigh = Color.Parse("#FF000000"),
        ChromeBlackLow = Color.Parse("#33000000"),
        ChromeBlackMedium = Color.Parse("#CC000000"),
        ChromeBlackMediumLow = Color.Parse("#66000000"),
        ChromeDisabledHigh = Color.Parse("#FFCFCFDE"),
        ChromeDisabledLow = Color.Parse("#FF6C664B"),
        ChromeGray = Color.Parse("#FF6C664B"),
        ChromeHigh = Color.Parse("#FFCFCFDE"),
        ChromeLow = Color.Parse("#FFF7F1DC"),
        ChromeMedium = Color.Parse("#FFF0E9D3"),
        ChromeMediumLow = Color.Parse("#FFFAF5E4"),
        ChromeWhite = Color.Parse("#FFFFFFFF"),
        ErrorText = Color.Parse("#FFCB3A2A"),
        ListLow = Color.Parse("#19000000"),
        ListMedium = Color.Parse("#33000000"),
        RegionColor = Color.Parse("#FFFFFBEB"),
    };
}
