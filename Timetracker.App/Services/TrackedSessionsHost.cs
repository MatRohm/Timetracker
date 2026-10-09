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
public sealed class TrackedSessionsHost : ITrackedSessionsQuery, ITrackedSessionsCommand
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
        string bookingElement)
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
            TaskId = TrackerEntry.ResolveTaskId(_tracker.SessionLog, name),
            BookingElement = element.Length > 0 ? element : TrackerEntry.LatestBookingElement(_tracker.SessionLog, name),
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
        IReadOnlyList<SessionChange> changes)
    {
        var current = _tracker.SessionLog;
        var mapped = new List<(TrackerEntry Original, TrackerEntry? Updated)>();
        foreach (var change in changes)
        {
            // Match the planned session back to the log by its stable identity; a
            // change whose original is no longer present is stale, so nothing is applied.
            var original = current.FirstOrDefault(e => e.Id == change.Original.Id);
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

    private static TrackedSession ToTracked(TrackerEntry entry) => new()
    {
        Id = entry.Id,
        Task = entry.Task,
        BookingElement = entry.BookingElement,
        Start = entry.Start,
        End = entry.End,
        Duration = entry.Duration,
        DurationSeconds = entry.DurationSeconds,
    };

    private static TrackerEntry FromTracked(TrackedSession session) => new()
    {
        Id = session.Id,
        Task = session.Task,
        BookingElement = session.BookingElement,
        Start = session.Start,
        End = session.End,
        Duration = session.Duration,
        DurationSeconds = session.DurationSeconds,
    };
}
