using Timetracker.App.Models;

namespace Timetracker.App.Services;

/// <summary>Outcome kind of an <see cref="EntryEditor"/> operation.</summary>
public enum EntryEditStatus
{
    /// <summary>Nothing to do: there were no sessions or changes.</summary>
    Declined,

    /// <summary>The change was persisted.</summary>
    Saved,

    /// <summary>Persisting failed; the file was left untouched.</summary>
    Failed,
}

/// <summary>
/// Result of an editor operation: the outcome, a short description of what was
/// affected (e.g. the task name) and, on failure, the exception.
/// </summary>
public sealed record EntryEditResult(EntryEditStatus Status, string Summary, Exception? Error = null);

/// <summary>
/// The session-log editing use cases: removing sessions, replacing a task's
/// sessions, renaming a task's sessions and applying planned changes. Works on
/// models only and persists the whole log through the injected delegate; the
/// calling view model owns confirmation, row state, the status line and events.
/// </summary>
public sealed class EntryEditor
{
    private readonly Func<IReadOnlyList<TrackerEntry>, Task> _saveEntries;
    private readonly Action<string, Exception> _logError;

    public EntryEditor(
        Func<IReadOnlyList<TrackerEntry>, Task> saveEntries,
        Action<string, Exception> logError)
    {
        _saveEntries = saveEntries;
        _logError = logError;
    }

    /// <summary>
    /// Removes <paramref name="toRemove"/> from <paramref name="sessions"/> and
    /// persists the remainder. Sessions are matched by task name and start. Returns
    /// <see cref="EntryEditStatus.Declined"/> when there is nothing to remove and
    /// <see cref="EntryEditStatus.Failed"/> (file untouched) when the save fails.
    /// </summary>
    public async Task<EntryEditResult> DeleteAsync(
        IReadOnlyList<TrackerEntry> toRemove,
        IReadOnlyList<TrackerEntry> sessions)
    {
        if (toRemove.Count == 0)
        {
            return new EntryEditResult(EntryEditStatus.Declined, "");
        }

        var summary = $"{toRemove.Count} sessions";
        var removeKeys = toRemove.Select(s => (s.Task, s.Start)).ToHashSet();
        var remaining = sessions
            .Where(s => !removeKeys.Contains((s.Task, s.Start)))
            .ToList();

        try
        {
            await _saveEntries(remaining);
            return new EntryEditResult(EntryEditStatus.Saved, summary);
        }
        catch (Exception ex)
        {
            _logError("DeleteEntries", ex);
            return new EntryEditResult(EntryEditStatus.Failed, summary, ex);
        }
    }

    /// <summary>
    /// Replaces every stored session of <paramref name="task"/> with
    /// <paramref name="replacements"/>, leaving other tasks untouched. Used by the
    /// per-item editor. Returns <see cref="EntryEditStatus.Failed"/> (file untouched)
    /// when the save fails.
    /// </summary>
    public async Task<EntryEditResult> ReplaceSessionsAsync(
        string task,
        IReadOnlyList<TrackerEntry> replacements,
        IReadOnlyList<TrackerEntry> currentSessions)
    {
        var key = task.Trim();
        var unaffected = currentSessions
            .Where(s => !s.Task.Trim().Equals(key, StringComparison.CurrentCultureIgnoreCase))
            .Select(s => s.Clone())
            .ToList();
        var updated = unaffected.Concat(replacements).ToList();

        try
        {
            await _saveEntries(updated);
            return new EntryEditResult(EntryEditStatus.Saved, key);
        }
        catch (Exception ex)
        {
            _logError("ReplaceSessions", ex);
            return new EntryEditResult(EntryEditStatus.Failed, key, ex);
        }
    }

    /// <summary>
    /// Gives <paramref name="taskSessions"/> the new task name and booking element
    /// and persists the whole log (<paramref name="sessions"/>, which contains them).
    /// Validation and change detection are the caller's; returns
    /// <see cref="EntryEditStatus.Failed"/> when the save fails.
    /// </summary>
    public async Task<EntryEditResult> UpdateTextAsync(
        IReadOnlyList<TrackerEntry> taskSessions,
        string task,
        string bookingElement,
        IReadOnlyList<TrackerEntry> sessions)
    {
        foreach (var session in taskSessions)
        {
            session.Task = task;
            session.BookingElement = bookingElement;
        }

        try
        {
            // Rewrite the file with the edited values; existing entries are preserved.
            await _saveEntries(sessions);
            return new EntryEditResult(EntryEditStatus.Saved, task);
        }
        catch (Exception ex)
        {
            _logError("UpdateEntryText", ex);
            return new EntryEditResult(EntryEditStatus.Failed, task, ex);
        }
    }

    /// <summary>
    /// Applies planned session changes (e.g. from distributing a gap) and persists
    /// the whole log: each change's original session is replaced by its update or
    /// removed, every other session is kept. Originals are matched by reference, as
    /// the plans are made from <paramref name="currentSessions"/> itself. Returns
    /// <see cref="EntryEditStatus.Declined"/> for no changes and
    /// <see cref="EntryEditStatus.Failed"/> (file untouched) when an original is no
    /// longer in the log or the save fails.
    /// </summary>
    public async Task<EntryEditResult> ApplyChangesAsync(
        IReadOnlyList<SessionChange> changes,
        IReadOnlyList<TrackerEntry> currentSessions)
    {
        if (changes.Count == 0)
        {
            return new EntryEditResult(EntryEditStatus.Declined, "");
        }

        var summary = $"{changes.Count} sessions";
        var byOriginal = changes.ToDictionary<SessionChange, TrackerEntry>(
            c => c.Original, ReferenceEqualityComparer.Instance);
        var matched = currentSessions.Count(s => byOriginal.ContainsKey(s));
        if (matched != byOriginal.Count)
        {
            var stale = new InvalidOperationException(
                "A changed session is no longer in the log; refresh and try again.");
            _logError("ApplyChanges", stale);
            return new EntryEditResult(EntryEditStatus.Failed, summary, stale);
        }

        var updated = currentSessions
            .Select(s => byOriginal.TryGetValue(s, out var change) ? change.Updated?.Clone() : s.Clone())
            .OfType<TrackerEntry>()
            .ToList();

        try
        {
            await _saveEntries(updated);
            return new EntryEditResult(EntryEditStatus.Saved, summary);
        }
        catch (Exception ex)
        {
            _logError("ApplyChanges", ex);
            return new EntryEditResult(EntryEditStatus.Failed, summary, ex);
        }
    }
}
