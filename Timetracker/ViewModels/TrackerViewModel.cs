using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Timetracker.ActivityMonitor;
using Timetracker.Models;
using Timetracker.Services;

namespace Timetracker.ViewModels;

public sealed class TrackerViewModel : ObservableObject, IDisposable
{
    private readonly ITrackerRepository _repository;
    private readonly IUiTimer _timer;
    private readonly Stopwatch _watch = new();

    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;

    private DateTimeOffset _startedAt;
    private bool _isRunning;
    private string _taskName = "";
    private string _previewBookingElement = "";
    private string _elapsedTimeText = "00:00:00";
    private string _statusText = "";
    private TrackerStatus _status = TrackerStatus.Info;
    private string _title = "Timetracker";

    private readonly BindingList<EntryRow> _entries = new();
    private readonly BindingList<SuggestionItem> _suggestions = new();
    private List<TrackerEntry> _sessions = [];
    private string _sortColumn = nameof(EntryRow.StartText);
    private bool _sortAscending;

    /// <summary>Rows shown in the history grid per page; paging appears above this size.</summary>
    private const int PageSize = 10;

    /// <summary>All rows in sort order; <see cref="_entries"/> holds only the current page.</summary>
    private List<EntryRow> _allRows = [];
    private int _currentPage = 1;
    private int _totalPages = 1;

    /// <summary>Raised when Start was attempted without a task name; the view shows a hint.</summary>
    public event Action? InvalidTaskName;

    /// <summary>Raised when saving an entry failed; the view shows the message.</summary>
    public event Action<string>? ErrorOccurred;

    public TrackerViewModel(ITrackerRepository repository, IUiTimer timer)
    {
        _repository = repository;
        _timer = timer;
        _timer.Tick += OnTimerTick;

        _startCommand = new RelayCommand(Start, () => !IsRunning);
        _stopCommand = new RelayCommand(Stop, () => IsRunning);
        _previousPageCommand = new RelayCommand(() => GoToPage(CurrentPage - 1), () => CurrentPage > 1);
        _nextPageCommand = new RelayCommand(() => GoToPage(CurrentPage + 1), () => CurrentPage < TotalPages);

        StatusText = "Entries are appended to " + _repository.FilePath;

        RefreshEntries();
    }

    public ICommand StartCommand => _startCommand;

    public ICommand StopCommand => _stopCommand;

    public string TaskName
    {
        get => _taskName;
        set
        {
            if (SetProperty(ref _taskName, value))
            {
                RefreshSuggestions();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        private set => SetProperty(ref _elapsedTimeText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Kind of the current status message; the view maps it to a color.</summary>
    public TrackerStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Booking element supplied by an integration (e.g. Azure DevOps) for the next
    /// started session; consumed and cleared by <see cref="BuildEntry"/> so it does
    /// not leak into later sessions of other tasks.
    /// </summary>
    public string PreviewBookingElement
    {
        get => _previewBookingElement;
        set => SetProperty(ref _previewBookingElement, value);
    }

    /// <summary>Tasks (grouped sessions) of the current history page; refreshed after every save.</summary>
    public BindingList<EntryRow> Entries => _entries;

    /// <summary>Autocomplete suggestions for the current task-name input.</summary>
    public BindingList<SuggestionItem> Suggestions => _suggestions;

    /// <summary>Week view: seven weekday columns with week navigation.</summary>
    public WeekViewModel Week { get; } = new();

    /// <summary>Property name the history list is currently sorted by.</summary>
    public string SortColumn
    {
        get => _sortColumn;
        private set => SetProperty(ref _sortColumn, value);
    }

    public bool SortAscending
    {
        get => _sortAscending;
        private set => SetProperty(ref _sortAscending, value);
    }

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    /// <summary>1-based page number shown in the history grid.</summary>
    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageText));
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(HasMultiplePages));
                OnPropertyChanged(nameof(PageText));
            }
        }
    }

    /// <summary>True when the history has more than one page of rows.</summary>
    public bool HasMultiplePages => TotalPages > 1;

    /// <summary>Text for the pager label, e.g. "Page 2 of 5".</summary>
    public string PageText => $"Page {CurrentPage} of {TotalPages}";

    /// <summary>Saves the running entry (if any); called by the view when the app is closing.</summary>
    public void SaveRunningEntryOnClose()
    {
        if (IsRunning)
            HaltAndSave();
    }

    /// <summary>Puts a suggestion's task name into the input field.</summary>
    public void AcceptSuggestion(SuggestionItem suggestion) => TaskName = suggestion.Name;

    /// <summary>
    /// Deletes the given rows (all their sessions) from the log after asking the
    /// user to confirm. Returns false when the user declined or the save failed.
    /// </summary>
    public bool DeleteEntries(IReadOnlyList<EntryRow> rows, Func<string, bool> confirm)
    {
        if (rows is null || rows.Count == 0)
        {
            return false;
        }

        var summary = rows.Count == 1
            ? $"\"{rows[0].Task}\" (all {rows[0].Sessions.Count} sessions)"
            : $"{rows.Count} tasks ({rows.Sum(r => r.Sessions.Count)} sessions)";
        if (!confirm(summary))
        {
            return false;
        }

        // Snapshot so a failed save can restore exactly the previous state.
        var backup = _sessions.Select(e => e.Clone()).ToList();
        var removeKeys = rows.SelectMany(r => r.Sessions)
            .Select(s => (s.Task, s.Start))
            .ToHashSet();

        var remaining = _sessions
            .Where(s => !removeKeys.Contains((s.Task, s.Start)))
            .ToList();

        try
        {
            _repository.Save(remaining);

            Status = TrackerStatus.Success;
            StatusText = $"✓ Deleted {summary}";
            RefreshEntries();
            return true;
        }
        catch (Exception ex)
        {
            // Roll back the in-memory state to the pre-delete snapshot.
            _sessions = backup;
            RefreshEntries();

            ErrorLog.Log("DeleteEntries", ex);
            Status = TrackerStatus.Error;
            StatusText = "✗ Delete failed: " + ex.Message;
            ErrorOccurred?.Invoke("Could not delete the entry:\n" + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Starts tracking the given history row (double-click in the list). Fills the
    /// task field with its name, so the new session continues that task, and starts
    /// the timer. Does nothing while the timer is already running.
    /// </summary>
    public void StartFromRow(EntryRow row)
    {
        if (IsRunning || row is null)
            return;

        TaskName = row.Task;
        Start();
    }

    /// <summary>
    /// Commits an inline text edit (task name / booking element) and persists the
    /// whole log. Returns false when the edit was rejected (empty task name or
    /// save failure).
    /// </summary>
    public bool UpdateEntryText(EntryRow row, string task, string bookingElement)
    {
        task = task.Trim();
        if (task.Length == 0)
        {
            InvalidTaskName?.Invoke();
            return false;
        }

        // Compare against the committed snapshot: the grid binding stages the edited
        // text in the row BEFORE this method runs, so row.Task/row.BookingElement
        // already hold the new values and cannot be used to detect a change.
        var originalTask = row.CommittedTask;
        var originalBookingElement = row.CommittedBookingElement;

        if (task == originalTask && bookingElement == originalBookingElement)
        {
            // Nothing changed; drop the staged edit and restore the committed text.
            row.CommitText(originalTask, originalBookingElement);
            return true;
        }

        foreach (var session in row.Sessions)
        {
            session.Task = task;
            session.BookingElement = bookingElement;
        }

        try
        {
            // Rewrite the file with the edited values; existing entries are preserved.
            _repository.Save(_sessions);

            row.CommitText(task, bookingElement);

            Status = TrackerStatus.Success;
            StatusText = $"✓ Updated \"{task}\"";

            RefreshEntries();
            RevealTask(task);
            return true;
        }
        catch (Exception ex)
        {
            // The file was not changed; rebuild the in-memory state from it.
            RefreshEntries();

            ErrorLog.Log("UpdateEntryText", ex);
            Status = TrackerStatus.Error;
            StatusText = "✗ Update failed: " + ex.Message;
            ErrorOccurred?.Invoke("Could not save the change:\n" + ex.Message);
            return false;
        }
    }

    public void Dispose() => _timer.Dispose();

    private void Start()
    {
        if (IsRunning)
            return;

        var task = TaskName.Trim();
        if (task.Length == 0)
        {
            InvalidTaskName?.Invoke();
            return;
        }
        _startedAt = DateTimeOffset.Now;
        _watch.Restart();
        _timer.Start();
        IsRunning = true;

        ElapsedTimeText = "00:00:00";
        Status = TrackerStatus.Info;
        StatusText = $"Tracking \"{task}\" since {_startedAt:HH:mm:ss} …";
        Title = "Timetracker – " + task;

        // The preview is consumed with the next save (see BuildEntry).
        RefreshCommands();
    }

    private void Stop()
    {
        if (!IsRunning)
            return;
        HaltAndSave();
    }

    private void HaltAndSave()
    {
        _timer.Stop();
        _watch.Stop();
        IsRunning = false;
        Title = "Timetracker";

        Save(BuildEntry(DateTimeOffset.Now, _watch.Elapsed));
        PreviewBookingElement = "";

        RefreshCommands();
    }

    private TrackerEntry BuildEntry(DateTimeOffset endedAt, TimeSpan elapsed) => new()
    {
        Task = TaskName.Trim(),
        // An integration-provided element wins for this session; otherwise the new
        // session inherits the task's latest booking element so grouping stays consistent.
        BookingElement = PreviewBookingElement.Length > 0
            ? PreviewBookingElement
            : _sessions
                .Where(e => e.Task.Trim().Equals(TaskName.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(e => e.BookingElement)
                .LastOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "",
        Start = _startedAt,
        End = endedAt,
        Duration = elapsed.ToString(@"hh\:mm\:ss"),
        DurationSeconds = Math.Round(elapsed.TotalSeconds, 1),
    };

    private void Save(TrackerEntry entry)
    {
        try
        {
            _repository.Add(entry);

            Status = TrackerStatus.Success;
            StatusText = $"✓ Saved {entry.Duration} to {_repository.FilePath}";

            RefreshEntries();
            RevealTask(entry.Task);
        }
        catch (Exception ex)
        {
            ErrorLog.Log("SaveEntry", ex);
            Status = TrackerStatus.Error;
            StatusText = "✗ Save failed: " + ex.Message;
            ErrorOccurred?.Invoke("Could not save the entry:\n" + ex.Message);
        }
    }

    /// <summary>
    /// Sorts the history list. Clicking the same column again toggles the direction;
    /// a new column starts with dates newest-first, everything else ascending.
    /// Non-sortable columns (BookingElement) are ignored, so clicking them keeps the
    /// current sort and never produces a sort glyph on a NotSortable column.
    /// </summary>
    public void ApplySort(string column)
    {
        if (column == nameof(EntryRow.BookingElement))
        {
            return;
        }

        if (column == SortColumn)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = column is not (nameof(EntryRow.StartText) or nameof(EntryRow.EndText));
        }

        SortEntries();
    }

    private void SortEntries()
    {
        var rows = _allRows.ToList();
        rows.Sort(CompareRows);
        FillEntries(rows);
    }

    private void RefreshEntries()
    {
        _sessions = [.. _repository.GetAll()];

        // One row per distinct task name; durations are summed across its sessions.
        var rows = _sessions
            .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new EntryRow([.. g]))
            .ToList();
        rows.Sort(CompareRows);
        FillEntries(rows);

        // Keep the week view in sync with the session log and text edits.
        Week.UpdateSessions(_sessions);
        Week.UpdateActivitySpans(new ActivityLog().GetAll());

        RefreshSuggestions();
    }

    private void FillEntries(IReadOnlyList<EntryRow> rows)
    {
        _allRows = [.. rows];
        TotalPages = Math.Max(1, (_allRows.Count + PageSize - 1) / PageSize);
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        var pageRows = _allRows
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // One reset instead of a change event per row, so the grid redraws in one go.
        Entries.RaiseListChangedEvents = false;
        Entries.Clear();
        foreach (var row in pageRows)
        {
            Entries.Add(row);
        }
        Entries.RaiseListChangedEvents = true;
        Entries.ResetBindings();

        RefreshPageCommands();
    }

    private void GoToPage(int page)
    {
        var target = Math.Clamp(page, 1, TotalPages);
        if (target == CurrentPage)
        {
            return;
        }

        CurrentPage = target;
        FillEntries(_allRows);
    }

    /// <summary>
    /// Flips to the page containing the row for the given task, so the row the user
    /// just saved or edited stays visible even if its sort position is on another page.
    /// </summary>
    private void RevealTask(string task)
    {
        var index = _allRows.FindIndex(r =>
            r.Task.Equals(task.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var page = index / PageSize + 1;
        if (page != CurrentPage)
        {
            CurrentPage = page;
            FillEntries(_allRows);
        }
    }

    private void RefreshPageCommands()
    {
        _previousPageCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSuggestions()
    {
        var typed = TaskName.Trim();

        Suggestions.RaiseListChangedEvents = false;
        Suggestions.Clear();

        if (typed.Length > 0)
        {
            var matches = _sessions
                .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new SuggestionItem(g.Key, g.Sum(e => e.DurationSeconds)))
                .Where(s => s.Name.Contains(typed, StringComparison.CurrentCultureIgnoreCase)
                            && !s.Name.Equals(typed, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.TotalSeconds)
                .Take(8);

            foreach (var suggestion in matches)
            {
                Suggestions.Add(suggestion);
            }
        }

        Suggestions.RaiseListChangedEvents = true;
        Suggestions.ResetBindings();
    }

    private int CompareRows(EntryRow a, EntryRow b)
    {
        var result = SortColumn switch
        {
            nameof(EntryRow.Task) => string.Compare(a.Task, b.Task, StringComparison.CurrentCultureIgnoreCase),
            nameof(EntryRow.Duration) => a.DurationSeconds.CompareTo(b.DurationSeconds),
            nameof(EntryRow.EndText) => a.End.CompareTo(b.End),
            _ => a.Start.CompareTo(b.Start),
        };

        return SortAscending ? result : -result;
    }

    private void OnTimerTick()
    {
        if (_watch.IsRunning)
        {
            ElapsedTimeText = _watch.Elapsed.ToString(@"hh\:mm\:ss");
        }
    }

    private void RefreshCommands()
    {
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
    }
}
