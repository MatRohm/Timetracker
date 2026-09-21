using System.Collections.ObjectModel;
using Timetracker.Models;

namespace Timetracker.ViewModels;

/// <summary>
/// State and behavior of the per-item editor dialog: shows every session of one
/// tracking item (a history row's task), lets each session's start/end be edited
/// with the duration re-derived, and deletes individual sessions.
/// Independent of the UI so it can be tested without an Avalonia application.
/// </summary>
public sealed class EditEntriesViewModel : ObservableObject
{
    private readonly ObservableCollection<SessionEditRow> _sessions = [];
    private bool _sessionsModified;

    public EditEntriesViewModel(EntryRow item)
    {
        TaskName = string.IsNullOrWhiteSpace(item.Task) ? "(without task)" : item.Task;

        // Chronological, so the list reads like a timeline.
        foreach (var session in item.Sessions.OrderBy(s => s.Start))
        {
            _sessions.Add(new SessionEditRow(session));
        }
    }

    /// <summary>Task name of the edited item, shown as the dialog's subject.</summary>
    public string TaskName { get; }

    /// <summary>One editable row per stored session, oldest first.</summary>
    public ObservableCollection<SessionEditRow> Sessions => _sessions;

    /// <summary>True when at least one row was deleted, so the caller must persist.</summary>
    public bool SessionsModified
    {
        get => _sessionsModified;
        private set => SetProperty(ref _sessionsModified, value);
    }

    /// <summary>True when any edited row has an end before its start.</summary>
    public bool HasErrors => Sessions.Any(s => !s.IsValid);

    /// <summary>Saving is possible while there is something to save and no invalid row.</summary>
    public bool CanSave => Sessions.Count > 0 && !HasErrors;

    /// <summary>Summary line, e.g. "2 sessions · 01:30:00".</summary>
    public string SummaryText
    {
        get
        {
            var count = Sessions.Count;
            var total = TimeSpan.FromSeconds(Sessions.Sum(s => s.DurationSeconds));
            var noun = count == 1 ? "session" : "sessions";
            return $"{count} {noun} · {total:hh\\:mm\\:ss}";
        }
    }

    /// <summary>
    /// Removes one session from the item. Returns false when it was not part of
    /// the item. The underlying entry is only removed from storage by the caller.
    /// </summary>
    public bool RemoveSession(SessionEditRow session)
    {
        var removed = Sessions.Remove(session);
        if (removed)
        {
            SessionsModified = true;
            RaiseTotalsChanged();
        }
        return removed;
    }

    /// <summary>
    /// Writes the staged times of every row to its session. Returns false when a
    /// row is invalid, leaving all sessions untouched.
    /// </summary>
    public bool Save()
    {
        if (!CanSave)
        {
            return false;
        }

        foreach (var session in Sessions)
        {
            session.Commit();
        }
        return true;
    }

    /// <summary>The sessions to keep, in the item's original order.</summary>
    public IReadOnlyList<TrackerEntry> RemainingEntries() =>
        [.. Sessions.Select(s => s.Entry).OrderBy(e => e.Start)];

    private void RaiseTotalsChanged() => OnPropertyChanged(nameof(SummaryText));
}
