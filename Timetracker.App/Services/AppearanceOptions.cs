namespace Timetracker.App.Services;

/// <summary>
/// The core appearance options: their store key and the stable values written to
/// the options file, independent of the localized labels shown in the options view.
/// </summary>
public static class AppearanceOptions
{
    /// <summary>Store key of the Light/Dark theme setting.</summary>
    public const string ThemeKey = "General.Theme";

    /// <summary>Stored value of the light theme.</summary>
    public const string Light = "Light";

    /// <summary>Stored value of the dark theme.</summary>
    public const string Dark = "Dark";

    /// <summary>True when the stored value selects the dark theme.</summary>
    public static bool IsDark(string? value) =>
        string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase);
}
