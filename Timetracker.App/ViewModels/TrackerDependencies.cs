using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

/// <summary>
/// The infrastructure capabilities <see cref="TrackerViewModel"/> needs to read and
/// write entries, run the tracking timer and detect idle time. Every member is a
/// delegate rather than a service or interface type, so the view model stays free of
/// <c>Timetracker.App.Services</c> and <c>Timetracker.App.Interfaces</c> and only depends on
/// <see cref="Timetracker.App.Models"/> plus <c>System.*</c>. The composition root wires
/// these delegates to the concrete repository, timer and idle provider.
/// </summary>
public sealed record TrackerDependencies(
    Func<Task<IReadOnlyList<TrackerEntry>>> LoadEntries,
    Func<TrackerEntry, Task> AddEntry,
    Func<IReadOnlyList<TrackerEntry>, Task> SaveEntries,
    Func<string> FilePath,
    Action<Action> SubscribeTick,
    Action StartTimer,
    Action StopTimer,
    Action DisposeTimer,
    Func<TimeSpan> CurrentIdleTime,
    Action<string, Exception> LogError);
