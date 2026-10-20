using Avalonia.Media;

namespace Timetracker.Plugins.Contracts.Ui;

/// <summary>
/// Shared brushes for the view layer, so the same colors are defined once and used
/// by the app and every add-in's views. The brushes stay the same instances and are
/// recolored in place by <see cref="Apply"/>, so controls built before a theme
/// switch update with it.
/// </summary>
public static class ViewBrushes
{
    /// <summary>Success status text / accents.</summary>
    public static readonly IBrush Success = new SolidColorBrush(Color.FromRgb(0x14, 0x71, 0x0A));

    /// <summary>Error status text.</summary>
    public static readonly IBrush Error = new SolidColorBrush(Color.FromRgb(0xCB, 0x3A, 0x2A));

    /// <summary>Warnings that need attention, e.g. a day's untracked time.</summary>
    public static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xA3, 0x4D, 0x14));

    /// <summary>Caution amber, e.g. the monitor's "not installed" traffic-light state.</summary>
    public static readonly IBrush Yellow = new SolidColorBrush(Color.FromRgb(0x84, 0x6E, 0x15));

    /// <summary>Informational / secondary text.</summary>
    public static readonly IBrush Info = new SolidColorBrush(Color.FromRgb(0x69, 0x69, 0x69));

    /// <summary>Highlight behind the current day's column.</summary>
    public static readonly IBrush Today = new SolidColorBrush(Color.FromRgb(0xF2, 0xE6, 0xC9));

    /// <summary>No background.</summary>
    public static readonly IBrush Surface = Brushes.Transparent;

    /// <summary>Normal foreground for drawn symbols that do not inherit a text color.</summary>
    public static readonly IBrush Text = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));

    /// <summary>The folder symbol of the options view.</summary>
    public static readonly IBrush Folder = new SolidColorBrush(Color.FromRgb(0xD1, 0x8A, 0x00));

    /// <summary>Background of a tracked session block in the calendar.</summary>
    public static readonly IBrush CalendarBlock = new SolidColorBrush(Color.FromRgb(0xE7, 0xE1, 0xF7));

    /// <summary>Border of a tracked session block in the calendar.</summary>
    public static readonly IBrush CalendarBlockBorder = new SolidColorBrush(Color.FromRgb(0x64, 0x4A, 0xC9));

    /// <summary>Separator lines in the calendar.</summary>
    public static readonly IBrush CalendarGridLine = new SolidColorBrush(Color.FromArgb(0x30, 0x69, 0x69, 0x69));

    /// <summary>Background of a month cell.</summary>
    public static readonly IBrush CalendarCell = new SolidColorBrush(Color.FromRgb(0xFA, 0xF8, 0xEF));

    /// <summary>Background of a session chip in the month view.</summary>
    public static readonly IBrush CalendarChip = new SolidColorBrush(Color.FromRgb(0xE7, 0xE1, 0xF7));

    /// <summary>Background band of a day's untracked time.</summary>
    public static readonly IBrush GapBand = new SolidColorBrush(Color.FromArgb(0x30, 0xC0, 0x56, 0x00));

    private static readonly SolidColorBrush SuccessBrush = (SolidColorBrush)Success;
    private static readonly SolidColorBrush ErrorBrush = (SolidColorBrush)Error;
    private static readonly SolidColorBrush WarningBrush = (SolidColorBrush)Warning;
    private static readonly SolidColorBrush YellowBrush = (SolidColorBrush)Yellow;
    private static readonly SolidColorBrush InfoBrush = (SolidColorBrush)Info;
    private static readonly SolidColorBrush TodayBrush = (SolidColorBrush)Today;
    private static readonly SolidColorBrush TextBrush = (SolidColorBrush)Text;
    private static readonly SolidColorBrush FolderBrush = (SolidColorBrush)Folder;
    private static readonly SolidColorBrush CalendarBlockBrush = (SolidColorBrush)CalendarBlock;
    private static readonly SolidColorBrush CalendarBlockBorderBrush = (SolidColorBrush)CalendarBlockBorder;
    private static readonly SolidColorBrush CalendarGridLineBrush = (SolidColorBrush)CalendarGridLine;
    private static readonly SolidColorBrush CalendarCellBrush = (SolidColorBrush)CalendarCell;
    private static readonly SolidColorBrush CalendarChipBrush = (SolidColorBrush)CalendarChip;
    private static readonly SolidColorBrush GapBandBrush = (SolidColorBrush)GapBand;

    /// <summary>
    /// Recolors every semantic brush for the given theme: the Dracula palette for
    /// dark and its Alucard counterpart for light.
    /// </summary>
    public static void Apply(bool dark)
    {
        SuccessBrush.Color = dark ? Color.FromRgb(0x50, 0xFA, 0x7B) : Color.FromRgb(0x14, 0x71, 0x0A);
        ErrorBrush.Color = dark ? Color.FromRgb(0xFF, 0x55, 0x55) : Color.FromRgb(0xCB, 0x3A, 0x2A);
        WarningBrush.Color = dark ? Color.FromRgb(0xFF, 0xB8, 0x6C) : Color.FromRgb(0xA3, 0x4D, 0x14);
        YellowBrush.Color = dark ? Color.FromRgb(0xF1, 0xFA, 0x8C) : Color.FromRgb(0x84, 0x6E, 0x15);
        InfoBrush.Color = dark ? Color.FromRgb(0x9A, 0xA3, 0xC7) : Color.FromRgb(0x69, 0x69, 0x69);
        TodayBrush.Color = dark ? Color.FromRgb(0x44, 0x47, 0x5A) : Color.FromRgb(0xF2, 0xE6, 0xC9);
        TextBrush.Color = dark ? Color.FromRgb(0xF8, 0xF8, 0xF2) : Color.FromRgb(0x1F, 0x1F, 0x1F);
        FolderBrush.Color = dark ? Color.FromRgb(0xFF, 0xB8, 0x6C) : Color.FromRgb(0xD1, 0x8A, 0x00);
        CalendarBlockBrush.Color = dark ? Color.FromRgb(0x44, 0x47, 0x5A) : Color.FromRgb(0xE7, 0xE1, 0xF7);
        CalendarBlockBorderBrush.Color = dark ? Color.FromRgb(0x62, 0x72, 0xA4) : Color.FromRgb(0x64, 0x4A, 0xC9);
        CalendarGridLineBrush.Color = dark
            ? Color.FromArgb(0x40, 0x62, 0x72, 0xA4)
            : Color.FromArgb(0x30, 0x69, 0x69, 0x69);
        CalendarCellBrush.Color = dark ? Color.FromRgb(0x2F, 0x32, 0x41) : Color.FromRgb(0xFA, 0xF8, 0xEF);
        CalendarChipBrush.Color = dark ? Color.FromRgb(0x44, 0x47, 0x5A) : Color.FromRgb(0xE7, 0xE1, 0xF7);
        GapBandBrush.Color = dark
            ? Color.FromArgb(0x30, 0xFF, 0xB8, 0x6C)
            : Color.FromArgb(0x30, 0xC0, 0x56, 0x00);
    }
}
