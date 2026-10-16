namespace Timetracker.Plugins.Contracts;

/// <summary>
/// Payload of <see cref="Interfaces.ITrackerStateQuery.StateChanged"/>: the new
/// tracking state and the session it concerns. On <see cref="TrackerState.Running"/>
/// the session is provisional (<see cref="TrackedSession.Id"/> is
/// <see cref="Guid.Empty"/> and <see cref="TrackedSession.End"/> is its default);
/// on <see cref="TrackerState.Stopped"/> it is the stored session.
/// </summary>
public sealed record TrackerStateChangedEventArgs(TrackerState State, TrackedSession Session);
