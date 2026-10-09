using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.ViewModels;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// State of the "book untracked time" dialog: the gap's start and end (editable,
/// in the per-item editor's "yyyy-MM-dd HH:mm" format), the task name with the
/// usual suggestions and an optional booking element. The caller books
/// <see cref="Range"/> once the user confirms.
/// </summary>
public sealed class BookGapViewModel : ObservableObject
{
    private readonly IReadOnlyList<TrackedSession> _sessions;
    private string _taskName = "";
    private string _bookingElement = "";

    public BookGapViewModel(TimeRange gap, IReadOnlyList<TrackedSession> sessions)
    {
        _sessions = sessions;

        // A detached entry only carries the times; nothing is written back to it.
        Times = new SessionEditRow("", gap.Start, gap.End);
        Times.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanBook));
    }

    /// <summary>Editable start and end of the stretch to book; the duration follows.</summary>
    public SessionEditRow Times { get; }

    /// <summary>Suggestions for <see cref="TaskName"/>, from every saved session.</summary>
    public SuggestionListViewModel Suggestions { get; } = new();

    public string TaskName
    {
        get => _taskName;
        set
        {
            if (SetProperty(ref _taskName, value))
            {
                Suggestions.Refresh(value, _sessions.Select(e => (e.Task, e.DurationSeconds)));
                OnPropertyChanged(nameof(CanBook));
            }
        }
    }

    /// <summary>Optional; empty means the task's latest booking element is used.</summary>
    public string BookingElement
    {
        get => _bookingElement;
        set => SetProperty(ref _bookingElement, value);
    }

    /// <summary>True when a task name is entered and the times form a valid, non-empty range.</summary>
    public bool CanBook => TaskName.Trim().Length > 0 && Times.IsValid && Times.End > Times.Start;

    /// <summary>The stretch to book, from the staged times.</summary>
    public TimeRange Range => new(Times.Start, Times.End);

    /// <summary>Takes a suggestion's task name and hides the list.</summary>
    public void AcceptSuggestion(SuggestionItem suggestion)
    {
        TaskName = suggestion.Name;
        Suggestions.Refresh("", _sessions.Select(e => (e.Task, e.DurationSeconds)));
    }
}
