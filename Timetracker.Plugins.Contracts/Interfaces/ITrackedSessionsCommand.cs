namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Changes the stored sessions (booking a new one and applying planned edits),
/// separate from the start/stop surface of <see cref="ITrackerSessionCommand"/>.
/// Implemented by the main app; add-in components (e.g. the week view) resolve it
/// via the DI container.
/// </summary>
public interface ITrackedSessionsCommand
{
    /// <summary>Books <paramref name="range"/> as a new session; false when it was not saved.</summary>
    Task<bool> BookAsync(TimeRange range, string task, string bookingElement);

    /// <summary>Applies the planned session changes and persists them; false when none were saved.</summary>
    Task<bool> ApplyChangesAsync(IReadOnlyList<SessionChange> changes);
}
