using Avalonia.Media;

namespace Timetracker.Views.Components;

/// <summary>
/// Shared brushes for the view layer, so the same colors are defined once and used
/// by the tab views and their components.
/// </summary>
internal static class ViewBrushes
{
    /// <summary>Success status text / accents.</summary>
    public static readonly IBrush Success = new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22));

    /// <summary>Error status text.</summary>
    public static readonly IBrush Error = new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22));

    /// <summary>Informational / secondary text.</summary>
    public static readonly IBrush Info = new SolidColorBrush(Color.FromRgb(0x69, 0x69, 0x69));

    /// <summary>Highlight behind the current day's column.</summary>
    public static readonly IBrush Today = new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xB5));

    /// <summary>No background.</summary>
    public static readonly IBrush Surface = Brushes.Transparent;
}
