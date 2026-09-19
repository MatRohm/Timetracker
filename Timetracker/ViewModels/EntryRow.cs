using Timetracker.Models;

namespace Timetracker.ViewModels;

/// <summary>
/// Display and edit row for one task in the history list. A row aggregates every
/// session (<see cref="TrackerEntry"/>) with the same task name, so repeated work
/// on a task shows its accumulated duration. Text edits are staged here by the
/// grid binding and committed via <see cref="TrackerViewModel.UpdateEntryText"/>.
/// </summary>
public sealed class EntryRow : ObservableObject
{
    private readonly IReadOnlyList<TrackerEntry> _sessions;
    private string _task;
    private string _description;

    /// <summary>Text as last committed to the repository; edits are compared against this.</summary>
    private string _committedTask;
    private string _committedDescription;

    public EntryRow(IReadOnlyList<TrackerEntry> sessions)
    {
        _sessions = sessions;
        var latest = sessions[^1];
        _task = latest.Task;
        _description = sessions
            .Select(e => e.Description)
            .LastOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? "";
        _committedTask = _task;
        _committedDescription = _description;

        Start = sessions.Min(e => e.Start);
        End = sessions.Max(e => e.End);
        DurationSeconds = Math.Round(sessions.Sum(e => e.DurationSeconds), 1);
        Duration = TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss");
    }

    /// <summary>All sessions this row aggregates; text edits apply to each of them.</summary>
    public IReadOnlyList<TrackerEntry> Sessions => _sessions;

    public string Task
    {
        get => _task;
        set => SetProperty(ref _task, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>Task name as last persisted (staged edits are not included).</summary>
    public string CommittedTask => _committedTask;

    /// <summary>Description as last persisted (staged edits are not included).</summary>
    public string CommittedDescription => _committedDescription;

    /// <summary>First time any session of this task started.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Last time any session of this task ended.</summary>
    public DateTimeOffset End { get; }

    /// <summary>Total time of all sessions, formatted hh:mm:ss.</summary>
    public string Duration { get; }

    /// <summary>Sort key for the duration column (total seconds).</summary>
    public double DurationSeconds { get; }

    /// <summary>Display text of <see cref="Start"/>.</summary>
    public string StartText => Start.ToString("yyyy-MM-dd HH:mm");

    /// <summary>Display text of <see cref="End"/>.</summary>
    public string EndText => End.ToString("yyyy-MM-dd HH:mm");

    /// <summary>
    /// Marks the staged text as persisted. The staged values already match, so this
    /// only updates the commit snapshot and refreshes display bindings.
    /// </summary>
    public void CommitText(string task, string description)
    {
        _committedTask = task;
        _committedDescription = description;
        OnPropertyChanged(nameof(CommittedTask));
        OnPropertyChanged(nameof(CommittedDescription));
        OnPropertyChanged(nameof(Task));
        OnPropertyChanged(nameof(Description));
    }
}
