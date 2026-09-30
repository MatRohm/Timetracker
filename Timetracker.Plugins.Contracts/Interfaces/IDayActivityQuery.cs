namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Supplies how long the computer was actively used on a day (e.g. from the PC
/// activity monitor). The week view compares it with the booked time to show how
/// much of the day was not tracked.
/// </summary>
public interface IDayActivityQuery
{
    /// <summary>Active time on the given day; <see cref="TimeSpan.Zero"/> when nothing was recorded.</summary>
    TimeSpan GetActiveTime(DateOnly day);

    /// <summary>
    /// The day's active stretches, clipped to the day and ordered by start; empty
    /// when nothing was recorded. Lets the week view show when time went untracked.
    /// </summary>
    IReadOnlyList<ActiveSpan> GetActiveSpans(DateOnly day);
}
