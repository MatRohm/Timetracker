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
    private string _bookingElement;

    /// <summary>Text as last committed to the repository; edits are compared against this.</summary>
    private string _committedTask;
    private string _committedBookingElement;

    public EntryRow(IReadOnlyList<TrackerEntry> sessions)
    {
        _sessions = sessions;
        var latest = sessions.Count > 0 ? sessions[^1] : null;
        _task = latest?.Task ?? "";
        _bookingElement = sessions
            .Select(e => e.BookingElement)
            .LastOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";
        _committedTask = _task;
        _committedBookingElement = _bookingElement;

        if (sessions.Count > 0)
        {
            Start = sessions.Min(e => e.Start);
            End = sessions.Max(e => e.End);
            DurationSeconds = Math.Round(sessions.Sum(e => e.DurationSeconds), 1);
        }
        else
        {
            Start = default;
            End = default;
            DurationSeconds = 0;
        }
        Duration = TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss");
    }

    /// <summary>All sessions this row aggregates; text edits apply to each of them.</summary>
    public IReadOnlyList<TrackerEntry> Sessions => _sessions;

    public string Task
    {
        get => _task;
        set => SetProperty(ref _task, value);
    }

    public string BookingElement
    {
        get => _bookingElement;
        set => SetProperty(ref _bookingElement, value);
    }

    /// <summary>Task name as last persisted (staged edits are not included).</summary>
    public string CommittedTask => _committedTask;

    /// <summary>Booking element as last persisted (staged edits are not included).</summary>
    public string CommittedBookingElement => _committedBookingElement;

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
    public void CommitText(string task, string bookingElement)
    {
        _committedTask = task;
        _committedBookingElement = bookingElement;
        OnPropertyChanged(nameof(CommittedTask));
        OnPropertyChanged(nameof(CommittedBookingElement));
        OnPropertyChanged(nameof(Task));
        OnPropertyChanged(nameof(BookingElement));
    }
}
