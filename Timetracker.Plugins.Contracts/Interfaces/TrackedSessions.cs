namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Reads the stored sessions and the running timer, separate from the start/stop
/// surface of <see cref="ITrackerSessionCommand"/>. Implemented by the main app;
/// add-in components (e.g. the week view) resolve it via the DI container.
/// </summary>
public interface ITrackedSessionsQuery
{
    /// <summary>Every stored session, most recent first.</summary>
    IReadOnlyList<TrackedSession> Sessions { get; }

    /// <summary>Start of the running timer, or null when none runs.</summary>
    DateTimeOffset? RunningSince { get; }

    /// <summary>Raised after sessions are saved, started or stopped.</summary>
    event EventHandler? Changed;
}

/// <summary>
/// Changes the stored sessions (booking a new one and applying planned edits),
/// separate from the start/stop surface of <see cref="ITrackerSessionCommand"/>.
/// Implemented by the main app; add-in components (e.g. the week view) resolve it
/// via the DI container.
/// </summary>
public interface ITrackedSessionsCommand
{
    /// <summary>Books <paramref name="range"/> as a new session; false when it was not saved.</summary>
    Task<bool> BookAsync(TimeRange range, string task, string bookingElement, CancellationToken cancellationToken = default);

    /// <summary>Applies the planned session changes and persists them; false when none were saved.</summary>
    Task<bool> ApplyChangesAsync(IReadOnlyList<SessionChange> changes, CancellationToken cancellationToken = default);
}
