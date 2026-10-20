namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// An immutable snapshot of everything a calendar rebuild produces: the seven day
/// columns (Monday first), the fitted axis, the now-line offset, the month grid and
/// the booked seconds of the visible span.
/// </summary>
public sealed record CalendarContent(
    IReadOnlyList<CalendarDayContent> Days,
    CalendarAxis Axis,
    double? NowLineMinutes,
    IReadOnlyList<MonthDayViewModel> MonthDays,
    double VisibleSeconds);
