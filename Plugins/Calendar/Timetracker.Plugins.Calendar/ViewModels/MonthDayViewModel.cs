using System.Globalization;
using Timetracker.Plugins.Calendar.Localization;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// One cell of the month grid: the day number, its booked total and up to a few
/// task chips. Days outside the shown month are kept for the complete grid.
/// </summary>
public sealed class MonthDayViewModel(
    DateTimeOffset date,
    bool isInMonth,
    bool isToday,
    double totalSeconds,
    IReadOnlyList<string> chips,
    int hiddenChipCount)
{
    /// <summary>The cell's date.</summary>
    public DateTimeOffset Date { get; } = date;

    /// <summary>Day of month, zero-padded, e.g. "04".</summary>
    public string DayNumber { get; } = date.Day.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>False for the leading/trailing days of the grid.</summary>
    public bool IsInMonth { get; } = isInMonth;

    /// <summary>True for today; the view highlights it.</summary>
    public bool IsToday { get; } = isToday;

    /// <summary>Booked seconds that fall inside this cell's day.</summary>
    public double TotalSeconds { get; } = totalSeconds;

    /// <summary>Day total, e.g. "Σ 6:30"; empty when nothing was booked.</summary>
    public string TotalText { get; } = totalSeconds > 0
        ? $"Σ {CalendarTimeFormat.HoursMinutes(totalSeconds)}"
        : "";

    /// <summary>Task names shown as chips, in start order.</summary>
    public IReadOnlyList<string> Chips { get; } = chips;

    /// <summary>Number of tasks that did not fit the chip cap.</summary>
    public int HiddenChipCount { get; } = hiddenChipCount;

    /// <summary>True when <see cref="MoreText"/> should be shown.</summary>
    public bool HasMore => HiddenChipCount > 0;

    /// <summary>Overflow caption, e.g. "+2 more"; empty when all chips fit.</summary>
    public string MoreText => HiddenChipCount > 0
        ? string.Format(Strings.Calendar_MoreChips, HiddenChipCount)
        : "";
}
