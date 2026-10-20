namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Contributes data lines shown per weekday in the calendar (like the PC
/// activity line). Implement this to add further per-day summaries.
/// </summary>
public interface ICalendarDayQuery
{
    /// <summary>Text for the given day, e.g. "PC 7:15 active"; empty when none.</summary>
    string GetDayText(DateOnly day);
}
