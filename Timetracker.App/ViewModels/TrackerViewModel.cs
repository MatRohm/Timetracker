using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

public sealed class TrackerViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// A running session is stopped automatically once there has been no keyboard
    /// or mouse input for this long. The recorded end is back-dated to when the
    /// idle stretch began, so idle time is not billed to the task.
    /// </summary>
    public static readonly TimeSpan IdleStopThreshold = TimeSpan.FromMinutes(30);

    private readonly TrackerDependencies _dependencies;
    private readonly Stopwatch _watch = new();

    /// <summary>Current time; replaceable so idle behavior can be tested deterministically.</summary>
    private readonly Func<DateTimeOffset> _now;

    private readonly RelayCommand _startCommand;
    private readonly AsyncRelayCommand _stopCommand;

    private readonly SuggestionListViewModel _suggestions = new();
    private readonly HistoryListViewModel _history = new();
    private readonly EntryEditor _editor;

    private DateTimeOffset _startedAt;
    private bool _isRunning;
    private string _taskName = "";
    private string _previewBookingElement = "";
    private string _elapsedTimeText = "00:00:00";
    private string _statusText = "";
    private TrackerStatus _status = TrackerStatus.Info;
    private string _title = "Timetracker";

    private List<TrackerEntry> _sessions = [];

    /// <summary>Raised when Start was attempted without a task name; the view shows a hint.</summary>
    public event Action? InvalidTaskName;

    /// <summary>Raised when saving an entry failed; the view shows the message.</summary>
    public event Action<string>? ErrorOccurred;

    public TrackerViewModel(
        TrackerDependencies dependencies,
        Func<DateTimeOffset>? now = null)
    {
        _dependencies = dependencies;
        _now = now ?? (() => DateTimeOffset.Now);
        _dependencies.SubscribeTick(OnTimerTick);

        // Re-raise the suggestion list's changes so bindings on this view model
        // (Suggestions / ShowSuggestions) stay live through the forward.
        _suggestions.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        // Same for the history list: the grid binds to this view model's forwards.
        _history.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        _editor = new EntryEditor(_dependencies.SaveEntries, _dependencies.LogError);

        _startCommand = new RelayCommand(Start, () => !IsRunning);
        _stopCommand = new AsyncRelayCommand(Stop, () => IsRunning);

        StatusText = "Entries are appended to " + _dependencies.FilePath();

        // Load the history in the background; Avalonia posts the continuation back
        // to the UI thread, so the entries populate as soon as the read completes.
        InitialLoad = RefreshEntriesAsync();
    }

    /// <summary>
    /// The history load started by the constructor. Await it before reading
    /// <see cref="Entries"/> when no UI thread serializes the continuations (tests):
    /// a second refresh started meanwhile would fill the same collection concurrently.
    /// </summary>
    public Task InitialLoad { get; }

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

    /// <summary>
    /// Shows a status message from an add-in in the shared status line. Setting both
    /// the text and the kind drives the view's color mapping through the normal
    /// property-changed path.
    /// </summary>
    public void ShowStatus(string message, TrackerStatus kind)
    {
        Status = kind;
        StatusText = message;
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
    public ObservableCollection<EntryRow> Entries => _history.Entries;

    /// <summary>Autocomplete suggestions for the current task-name input.</summary>
    public ObservableCollection<SuggestionItem> Suggestions => _suggestions.Suggestions;

    /// <summary>True while the suggestion list should be shown below the task input.</summary>
    public bool ShowSuggestions => _suggestions.ShowSuggestions;

    /// <summary>Week view: seven weekday columns with week navigation.</summary>
    public WeekViewModel Week { get; } = new();

    /// <summary>Property name the history list is currently sorted by.</summary>
    public string SortColumn => _history.SortColumn;

    public bool SortAscending => _history.SortAscending;

    public ICommand PreviousPageCommand => _history.PreviousPageCommand;

    public ICommand NextPageCommand => _history.NextPageCommand;

    /// <summary>1-based page number shown in the history grid.</summary>
    public int CurrentPage => _history.CurrentPage;

    public int TotalPages => _history.TotalPages;

    /// <summary>True when the history has more than one page of rows.</summary>
    public bool HasMultiplePages => _history.HasMultiplePages;

    /// <summary>Text for the pager label, e.g. "Page 2 of 5".</summary>
    public string PageText => _history.PageText;

    /// <summary>
    /// Sets the filter for one column (by the <see cref="EntryRow"/> property name)
    /// and refreshes the list. Empty clears that column's filter. Filtering spans all
    /// pages, like sorting; a row must match every filled column filter.
    /// </summary>
    public void SetColumnFilter(string column, string filter) => _history.SetColumnFilter(column, filter);

    /// <summary>True while any column filter is set; the view highlights active funnels.</summary>
    public bool IsFilterActive => _history.IsFilterActive;

    /// <summary>True when the given column currently has a filter.</summary>
    public bool IsColumnFiltered(string column) => _history.IsColumnFiltered(column);

    /// <summary>
    /// Human-readable description of what a delete would remove, e.g.
    /// <c>"Report" (all 3 sessions)</c>. Exposed so the view can confirm
    /// asynchronously without blocking the UI thread.
    /// </summary>
    public static string BuildDeleteSummary(IReadOnlyList<EntryRow> rows) =>
        EntryEditor.BuildDeleteSummary(rows);

    /// <summary>Saves the running entry (if any); called by the view when the app is closing.</summary>
    public async Task SaveRunningEntryOnCloseAsync()
    {
        if (IsRunning)
            await HaltAndSaveAsync(_now(), _watch.Elapsed);
    }

    /// <summary>Puts a suggestion's task name into the input field.</summary>
    public void AcceptSuggestion(SuggestionItem suggestion) => TaskName = suggestion.Name;

    /// <summary>
    /// Deletes the given rows (all their sessions) from the log after asking the
    /// user to confirm. Returns false when the user declined or the save failed.
    /// </summary>
    public async Task<bool> DeleteEntriesAsync(IReadOnlyList<EntryRow> rows, Func<string, bool> confirm)
    {
        // Snapshot so a failed save can restore exactly the previous state.
        var backup = _sessions.Select(e => e.Clone()).ToList();
        var result = await _editor.DeleteAsync(rows, _sessions, confirm);

        switch (result.Status)
        {
            case EntryEditStatus.Declined:
                return false;

            case EntryEditStatus.Saved:
                Status = TrackerStatus.Success;
                StatusText = $"✓ Deleted {result.Summary}";
                await RefreshEntriesAsync();
                return true;

            default:
                // Roll back the in-memory state to the pre-delete snapshot.
                _sessions = backup;
                await RefreshEntriesAsync();

                Status = TrackerStatus.Error;
                StatusText = "✗ Delete failed: " + result.Error!.Message;
                ErrorOccurred?.Invoke("Could not delete the entry:\n" + result.Error.Message);
                return false;
        }
    }

    /// <summary>
    /// Persists the given sessions for an edited item (a history row's task) and
    /// refreshes every affected view. Used by the per-item editor: it replaces all
    /// stored sessions of that task with the edited/remaining ones.
    /// </summary>
    public async Task<bool> ReplaceSessionsAsync(string task, IReadOnlyList<SessionEditRow> sessions)
    {
        var backup = _sessions.Select(e => e.Clone()).ToList();
        var result = await _editor.ReplaceSessionsAsync(task, sessions, _sessions);

        if (result.Status == EntryEditStatus.Saved)
        {
            Status = TrackerStatus.Success;
            StatusText = $"✓ Updated \"{result.Summary}\"";
            await RefreshEntriesAsync();
            _history.RevealTask(result.Summary);
            return true;
        }

        _sessions = backup;
        await RefreshEntriesAsync();

        Status = TrackerStatus.Error;
        StatusText = "✗ Update failed: " + result.Error!.Message;
        ErrorOccurred?.Invoke("Could not save the changes:\n" + result.Error.Message);
        return false;
    }

    /// <summary>
    /// Starts tracking the given history row (the row's play button). Fills the
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
    public async Task<bool> UpdateEntryTextAsync(EntryRow row, string task, string bookingElement)
    {
        var result = await _editor.UpdateTextAsync(row, task, bookingElement, _sessions);

        switch (result.Status)
        {
            case EntryEditStatus.Saved:
                Status = TrackerStatus.Success;
                StatusText = $"✓ Updated \"{result.Summary}\"";
                await RefreshEntriesAsync();
                _history.RevealTask(result.Summary);
                return true;

            case EntryEditStatus.Unchanged:
                return true;

            case EntryEditStatus.InvalidTaskName:
                InvalidTaskName?.Invoke();
                return false;

            default:
                // The file was not changed; rebuild the in-memory state from it.
                await RefreshEntriesAsync();

                Status = TrackerStatus.Error;
                StatusText = "✗ Update failed: " + result.Error!.Message;
                ErrorOccurred?.Invoke("Could not save the change:\n" + result.Error.Message);
                return false;
        }
    }

    public void Dispose() => _dependencies.DisposeTimer();

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
        _startedAt = _now();
        _watch.Restart();
        _dependencies.StartTimer();
        IsRunning = true;

        ElapsedTimeText = "00:00:00";
        Status = TrackerStatus.Info;
        StatusText = $"Tracking \"{task}\" since {_startedAt:HH:mm:ss} …";
        Title = "Timetracker – " + task;

        // The preview is consumed with the next save (see BuildEntry).
        RefreshCommands();
    }

    private async Task Stop()
    {
        if (!IsRunning)
            return;
        await HaltAndSaveAsync(_now(), _watch.Elapsed);
    }

    private async Task HaltAndSaveAsync(DateTimeOffset endedAt, TimeSpan elapsed)
    {
        _dependencies.StopTimer();
        _watch.Stop();
        IsRunning = false;
        Title = "Timetracker";

        await SaveAsync(BuildEntry(endedAt, elapsed));
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

    private async Task SaveAsync(TrackerEntry entry)
    {
        try
        {
            await _dependencies.AddEntry(entry);

            Status = TrackerStatus.Success;
            StatusText = $"✓ Saved {entry.Duration} to {_dependencies.FilePath()}";

            await RefreshEntriesAsync();
            _history.RevealTask(entry.Task);
        }
        catch (Exception ex)
        {
            _dependencies.LogError("SaveEntry", ex);
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
    public void ApplySort(string column) => _history.ApplySort(column);

    public async Task RefreshEntriesAsync()
    {
        _sessions = [.. await _dependencies.LoadEntries()];

        // The history list groups the sessions into one row per task, applies the
        // active sort and filter, and pages the result.
        _history.SetSessions(_sessions);

        // Keep the week view in sync with the session log and text edits.
        Week.UpdateSessions(_sessions);

        RefreshSuggestions();
    }

    private void RefreshSuggestions() => _suggestions.Refresh(TaskName, _sessions);

    private async void OnTimerTick()
    {
        if (!_watch.IsRunning)
        {
            return;
        }

        // No input for the threshold: stop and bill only up to the last input.
        if (_dependencies.CurrentIdleTime() >= IdleStopThreshold)
        {
            await StopForIdleAsync();
            return;
        }

        ElapsedTimeText = _watch.Elapsed.ToString(@"hh\:mm\:ss");
    }

    /// <summary>
    /// Stops the running session because the machine has been idle for too long.
    /// The entry is back-dated to the last input, so the idle stretch is not
    /// counted as work; the status line explains what happened.
    /// </summary>
    private async Task StopForIdleAsync()
    {
        var idle = _dependencies.CurrentIdleTime();

        // The last input happened when the idle stretch began; the session ends
        // there, so the idle time is not billed to the task.
        var endedAt = _now() - idle;
        var worked = endedAt > _startedAt ? endedAt - _startedAt : TimeSpan.Zero;

        await HaltAndSaveAsync(endedAt, worked);

        // Replace the generic save status with an explanation of the idle stop.
        Status = TrackerStatus.Info;
        StatusText = $"⏸ Stopped after {FormatIdle(idle)} idle – saved {worked:hh\\:mm\\:ss}.";
    }

    private static string FormatIdle(TimeSpan idle)
    {
        var minutes = (int)Math.Round(idle.TotalMinutes);
        var result = minutes == 1 ? "1 min" : $"{minutes} min";
        return result;
    }

    private void RefreshCommands()
    {
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
    }
}
