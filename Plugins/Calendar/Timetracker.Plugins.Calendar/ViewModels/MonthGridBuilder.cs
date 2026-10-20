using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Builds the fixed 6x7 month grid: the shown month plus the leading and trailing
/// days that complete the weeks. Pure data, so the layout is testable without a
/// view.
/// </summary>
public static class MonthGridBuilder
{
    /// <summary>Rows in the month grid.</summary>
    public const int Weeks = 6;

    /// <summary>Columns in the month grid, Monday first.</summary>
    public const int DaysPerWeek = 7;

    /// <summary>Days in the month grid.</summary>
    public const int CellCount = Weeks * DaysPerWeek;

    /// <summary>Task chips shown per cell before the "+N more" caption.</summary>
    public const int MaxChips = 3;

    /// <summary>Builds all cells of the grid around <paramref name="monthStart"/>.</summary>
    public static IReadOnlyList<MonthDayViewModel> Build(
        DateTimeOffset monthStart,
        IEnumerable<TrackedSession> sessions,
        DateTimeOffset now)
    {
        var month = CalendarDates.FirstOfMonth(monthStart);
        var gridStart = CalendarDates.StartOfWeek(month);
        var normalized = sessions
            .Select(s => (Session: s, Start: s.Start, End: s.End > s.Start ? s.End : now))
            .ToList();

        var result = new List<MonthDayViewModel>(CellCount);
        for (var i = 0; i < CellCount; i++)
        {
            var date = gridStart.AddDays(i);
            var dayStart = date;
            var dayEnd = date.AddDays(1);

            var dayParts = normalized
                .Where(s => s.Start < dayEnd && s.End > dayStart)
                .OrderBy(s => s.Start)
                .ToList();

            var totalSeconds = 0.0;
            var chips = new List<string>();
            foreach (var part in dayParts)
            {
                var start = part.Start > dayStart ? part.Start : dayStart;
                var end = part.End < dayEnd ? part.End : dayEnd;
                totalSeconds += (end - start).TotalSeconds;

                var task = part.Session.Task.Trim();
                if (task.Length > 0 && !chips.Contains(task, StringComparer.CurrentCultureIgnoreCase))
                {
                    chips.Add(task);
                }
            }

            var hidden = Math.Max(0, chips.Count - MaxChips);
            var shown = chips.Take(MaxChips).ToList();
            result.Add(new MonthDayViewModel(
                date,
                date.Month == month.Month && date.Year == month.Year,
                date.Date == now.Date,
                totalSeconds,
                shown,
                hidden));
        }

        return result;
    }
}
