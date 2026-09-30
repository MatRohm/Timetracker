using Microsoft.Extensions.Logging;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;
using Timetracker.App.ViewModels;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.App.Services;

/// <summary>
/// Host implementation that lets add-ins (e.g. the week view) read and change the
/// stored sessions. It reuses the tracker view model's session log and its editor,
/// so the session data stays in one place while add-ins only see the contract.
/// </summary>
public sealed class TrackedSessionsHost : ITrackedSessions
{
    private readonly ITrackerRepository _repository;
    private readonly EntryEditor _editor;
    private readonly TrackerViewModel _tracker;
    private readonly ILogger<TrackedSessionsHost> _logger;

    public TrackedSessionsHost(
        ITrackerRepository repository,
        EntryEditor editor,
        TrackerViewModel tracker,
        ILogger<TrackedSessionsHost> logger)
    {
        _repository = repository;
        _editor = editor;
        _tracker = tracker;
        _logger = logger;

        _tracker.SessionsChanged += OnSessionsChanged;
    }

    /// <summary>Raised after sessions are saved, started or stopped.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<TrackedSession> Sessions => [.. _tracker.SessionLog.Select(ToTracked)];

    public DateTimeOffset? RunningSince => _tracker.RunningSince;

    public async Task<bool> BookAsync(
        TimeRange range,
        string task,
        string bookingElement,
        CancellationToken cancellationToken = default)
    {
        var name = task.Trim();
        if (name.Length == 0 || range.End <= range.Start)
        {
            return false;
        }

        var element = bookingElement.Trim();
        var entry = new TrackerEntry
        {
            Task = name,
            BookingElement = element.Length > 0 ? element : LatestBookingElement(name),
        };
        entry.Reschedule(range.Start, range.End);

        try
        {
            await _repository.AddAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not book the gap to {Task}", name);
            return false;
        }

        await _tracker.RefreshEntriesAsync();
        return true;
    }

    public async Task<bool> ApplyChangesAsync(
        IReadOnlyList<SessionChange> changes,
        CancellationToken cancellationToken = default)
    {
        var current = _tracker.SessionLog;
        var mapped = new List<(TrackerEntry Original, TrackerEntry? Updated)>();
        foreach (var change in changes)
        {
            // Match the planned session back to the log by its identity; a change
            // whose original is gone is stale and nothing is applied.
            var original = current.FirstOrDefault(
                e => e.Task == change.Original.Task && e.Start == change.Original.Start);
            if (original is null)
            {
                return false;
            }

            mapped.Add((original, change.Updated is { } updated ? FromTracked(updated) : null));
        }

        var result = await _editor.ApplyChangesAsync(mapped, current.ToList());
        if (result.Status != EntryEditStatus.Saved)
        {
            return false;
        }

        await _tracker.RefreshEntriesAsync();
        return true;
    }

    private void OnSessionsChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>The task's most recent non-empty booking element; empty when it has none.</summary>
    private string LatestBookingElement(string task) =>
        _tracker.SessionLog
            .Where(e => e.Task.Trim().Equals(task.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(e => e.BookingElement)
            .LastOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";

    private static TrackedSession ToTracked(TrackerEntry entry) => new()
    {
        Task = entry.Task,
        BookingElement = entry.BookingElement,
        Start = entry.Start,
        End = entry.End,
        Duration = entry.Duration,
        DurationSeconds = entry.DurationSeconds,
    };

    private static TrackerEntry FromTracked(TrackedSession session) => new()
    {
        Task = session.Task,
        BookingElement = session.BookingElement,
        Start = session.Start,
        End = session.End,
        Duration = session.Duration,
        DurationSeconds = session.DurationSeconds,
    };
}
