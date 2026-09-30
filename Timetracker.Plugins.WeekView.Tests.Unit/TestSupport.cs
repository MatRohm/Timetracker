using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.WeekView.Tests.Unit;

/// <summary>
/// In-memory <see cref="ITrackedSessionsQuery"/> and
/// <see cref="ITrackedSessionsCommand"/>: the session list and running state are
/// set directly, and the book/apply actions are wired as delegates.
/// </summary>
public sealed class FakeTrackedSessions : ITrackedSessionsQuery, ITrackedSessionsCommand
{
    private List<TrackedSession> _sessions = [];

    public IReadOnlyList<TrackedSession> Sessions => _sessions;

    public DateTimeOffset? RunningSince { get; set; }

    public event EventHandler? Changed;

    public Func<TimeRange, string, string, Task<bool>>? Book;

    public Func<IReadOnlyList<SessionChange>, Task<bool>>? ApplyChanges;

    /// <summary>Replaces the sessions and raises <see cref="Changed"/>, like a save would.</summary>
    public void SetSessions(params TrackedSession[] sessions)
    {
        _sessions = sessions.ToList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> BookAsync(TimeRange range, string task, string bookingElement, CancellationToken cancellationToken = default) =>
        Book is null ? Task.FromResult(false) : Book(range, task, bookingElement);

    public Task<bool> ApplyChangesAsync(IReadOnlyList<SessionChange> changes, CancellationToken cancellationToken = default) =>
        ApplyChanges is null ? Task.FromResult(false) : ApplyChanges(changes);
}

/// <summary>
/// In-memory <see cref="IDayActivityQuery"/>: the per-day active time and active
/// spans are set as delegates.
/// </summary>
public sealed class FakeDayActivitySource : IDayActivityQuery
{
    public Func<DateOnly, TimeSpan> ActiveTime { get; set; } = _ => TimeSpan.Zero;

    public Func<DateOnly, IReadOnlyList<ActiveSpan>> ActiveSpans { get; set; } = _ => [];

    public TimeSpan GetActiveTime(DateOnly day) => ActiveTime(day);

    public IReadOnlyList<ActiveSpan> GetActiveSpans(DateOnly day) => ActiveSpans(day);
}
