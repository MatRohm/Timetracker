using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

/// <summary>Outcome kind of an <see cref="EntryEditor"/> operation.</summary>
public enum EntryEditStatus
{
    /// <summary>Nothing to do: no rows, or the user declined the confirmation.</summary>
    Declined,

    /// <summary>An inline text edit was rejected because the task name was empty.</summary>
    InvalidTaskName,

    /// <summary>An inline text edit changed nothing; the staged text was restored.</summary>
    Unchanged,

    /// <summary>The change was persisted.</summary>
    Saved,

    /// <summary>Persisting failed; the file was left untouched.</summary>
    Failed,
}

/// <summary>
/// Result of an editor operation: the outcome, the affected task name (or the
/// delete summary) and, on failure, the exception. The view model maps this to the
/// shared status line and events; the editor itself never touches them.
/// </summary>
public sealed record EntryEditResult(EntryEditStatus Status, string Summary, Exception? Error = null);

/// <summary>
/// The history-editing use cases: deleting rows, replacing a task's sessions and
/// committing an inline text edit. A plain collaborator (not a view model): it
/// persists through the injected delegate and reports what happened, so the
/// calling <see cref="TrackerViewModel"/> keeps ownership of the session list,
/// status line and events. Kept separate so the edit logic can be tested without
/// a tracker.
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
    /// Human-readable description of what a delete would remove, e.g.
    /// <c>"Report" (all 3 sessions)</c>. Exposed so the view can confirm
    /// asynchronously without blocking the UI thread.
    /// </summary>
    public static string BuildDeleteSummary(IReadOnlyList<EntryRow> rows) =>
        rows.Count == 1
            ? $"\"{rows[0].Task}\" (all {rows[0].Sessions.Count} sessions)"
            : $"{rows.Count} tasks ({rows.Sum(r => r.Sessions.Count)} sessions)";

    /// <summary>
    /// Deletes the given rows' sessions from <paramref name="sessions"/> and persists
    /// the remainder, after asking the user to confirm. Returns <see cref="EntryEditStatus.Declined"/>
    /// when there is nothing to delete or the user declined, and
    /// <see cref="EntryEditStatus.Failed"/> (leaving the file untouched) when the save fails.
    /// </summary>
    public async Task<EntryEditResult> DeleteAsync(
        IReadOnlyList<EntryRow> rows,
        IReadOnlyList<TrackerEntry> sessions,
        Func<string, bool> confirm)
    {
        if (rows is null || rows.Count == 0)
        {
            return new EntryEditResult(EntryEditStatus.Declined, "");
        }

        var summary = BuildDeleteSummary(rows);
        if (!confirm(summary))
        {
            return new EntryEditResult(EntryEditStatus.Declined, summary);
        }

        var removeKeys = rows.SelectMany(r => r.Sessions)
            .Select(s => (s.Task, s.Start))
            .ToHashSet();

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
    /// Replaces every stored session of <paramref name="task"/> with the given
    /// sessions, leaving other tasks untouched. Used by the per-item editor. Returns
    /// <see cref="EntryEditStatus.Failed"/> (file untouched) when the save fails.
    /// </summary>
    public async Task<EntryEditResult> ReplaceSessionsAsync(
        string task,
        IReadOnlyList<SessionEditRow> sessions,
        IReadOnlyList<TrackerEntry> currentSessions)
    {
        var key = task.Trim();
        var unaffected = currentSessions
            .Where(s => !s.Task.Trim().Equals(key, StringComparison.CurrentCultureIgnoreCase))
            .Select(s => s.Clone())
            .ToList();
        var updated = unaffected.Concat(sessions.Select(s => s.Entry)).ToList();

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

    /// <summary>
    /// Commits an inline text edit (task name / booking element) and persists the
    /// whole log. Returns <see cref="EntryEditStatus.InvalidTaskName"/> for an empty
    /// task name and <see cref="EntryEditStatus.Unchanged"/> when nothing changed
    /// (the staged text is restored); <see cref="EntryEditStatus.Failed"/> leaves the
    /// file untouched.
    /// </summary>
    public async Task<EntryEditResult> UpdateTextAsync(
        EntryRow row,
        string task,
        string bookingElement,
        IReadOnlyList<TrackerEntry> sessions)
    {
        task = task.Trim();
        if (task.Length == 0)
        {
            return new EntryEditResult(EntryEditStatus.InvalidTaskName, "");
        }

        // Compare against the committed snapshot: the grid binding stages the edited
        // text in the row BEFORE this runs, so row.Task/row.BookingElement already
        // hold the new values and cannot be used to detect a change.
        var originalTask = row.CommittedTask;
        var originalBookingElement = row.CommittedBookingElement;

        if (task == originalTask && bookingElement == originalBookingElement)
        {
            // Nothing changed; drop the staged edit and restore the committed text.
            row.CommitText(originalTask, originalBookingElement);
            return new EntryEditResult(EntryEditStatus.Unchanged, task);
        }

        foreach (var session in row.Sessions)
        {
            session.Task = task;
            session.BookingElement = bookingElement;
        }

        try
        {
            // Rewrite the file with the edited values; existing entries are preserved.
            await _saveEntries(sessions);
            row.CommitText(task, bookingElement);
            return new EntryEditResult(EntryEditStatus.Saved, task);
        }
        catch (Exception ex)
        {
            _logError("UpdateEntryText", ex);
            return new EntryEditResult(EntryEditStatus.Failed, task, ex);
        }
    }
}
