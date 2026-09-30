using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;
using Timetracker.App.Services;
using Timetracker.Plugins.Contracts.ViewModels;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;

namespace Timetracker.App.ViewModels;

public sealed class TrackerViewModel : ObservableObject, IDisposable
{
    private readonly ITrackerRepository _repository;
    private readonly IUiTimer _timer;
    private readonly ILogger<TrackerViewModel> _logger;
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

    /// <summary>Raised after the session log or the running state changed.</summary>
    public event EventHandler? SessionsChanged;

    /// <param name="repository">Reads and writes the session log.</param>
    /// <param name="timer">Ticks on the UI thread while a session runs.</param>
    /// <param name="editor">Persists the session-log changes the view model drives.</param>
    /// <param name="logger">Receives failures to save or book entries.</param>
    /// <param name="now">Current time; replaceable so time-dependent behavior can be tested deterministically.</param>
    public TrackerViewModel(
        ITrackerRepository repository,
        IUiTimer timer,
        EntryEditor editor,
        ILogger<TrackerViewModel> logger,
        Func<DateTimeOffset>? now = null)
    {
        _repository = repository;
        _timer = timer;
        _editor = editor;
        _logger = logger;
        _now = now ?? (() => DateTimeOffset.Now);
        _timer.Tick += OnTimerTick;

        // Re-raise the suggestion list's changes so bindings on this view model
        // (Suggestions / ShowSuggestions) stay live through the forward.
        _suggestions.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        // Same for the history list: the grid binds to this view model's forwards.
        _history.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        _startCommand = new RelayCommand(Start, () => !IsRunning);
        _stopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);

        StatusText = "Entries are appended to " + _repository.FilePath;

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

    /// <summary>The current session log; read by the session host for the add-ins.</summary>
    public IReadOnlyList<TrackerEntry> SessionLog => _sessions;

    /// <summary>Start of the running timer, or null when none runs.</summary>
    public DateTimeOffset? RunningSince => IsRunning ? _startedAt : null;

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
        rows.Count == 1
            ? $"\"{rows[0].Task}\" (all {rows[0].Sessions.Count} sessions)"
            : $"{rows.Count} tasks ({rows.Sum(r => r.Sessions.Count)} sessions)";

    /// <summary>Saves the running entry (if any); called by the view when the app is closing.</summary>
    public async Task SaveRunningEntryOnCloseAsync()
    {
        if (IsRunning)
            await HaltAndSaveAsync(_now(), _watch.Elapsed);
    }

    /// <summary>Puts a suggestion's task name into the input field.</summary>
    public void AcceptSuggestion(SuggestionItem suggestion) => TaskName = suggestion.Name;

    /// <summary>
    /// Starts a session for the given task on an add-in's behalf (via the session
    /// host). Returns false when a session is already running or the task name is
    /// empty; the add-in decides how to react.
    /// </summary>
    public Task<bool> StartSessionAsync(string taskName, string bookingElement = "")
    {
        if (IsRunning)
        {
            return Task.FromResult(false);
        }

        var task = taskName.Trim();
        if (task.Length == 0)
        {
            return Task.FromResult(false);
        }

        TaskName = task;
        PreviewBookingElement = bookingElement;
        Start();
        return Task.FromResult(true);
    }

    /// <summary>
    /// Stops and saves the running session on an add-in's behalf (via the session
    /// host). The end is back-dated when <paramref name="endedAt"/> is given (e.g.
    /// an idle stop); <paramref name="reason"/>, if given, replaces the generic save
    /// status. Returns false when no session is running.
    /// </summary>
    public async Task<bool> StopSessionAsync(DateTimeOffset? endedAt = null, string? reason = null)
    {
        if (!IsRunning)
        {
            return false;
        }

        var end = endedAt ?? _now();
        var elapsed = endedAt is null
            ? _watch.Elapsed
            : (end > _startedAt ? end - _startedAt : TimeSpan.Zero);

        await HaltAndSaveAsync(end, elapsed);

        if (reason is not null)
        {
            Status = TrackerStatus.Info;
            StatusText = reason;
        }

        return true;
    }

    /// <summary>
    /// Deletes the given rows (all their sessions) from the log. The caller asks
    /// the user to confirm first (via <see cref="Confirmations"/>), so this only
    /// runs once the user has agreed. Returns false when the save failed.
    /// </summary>
    public async Task<bool> DeleteEntriesAsync(IReadOnlyList<EntryRow> rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return false;
        }

        var summary = BuildDeleteSummary(rows);

        // Snapshot so a failed save can restore exactly the previous state.
        var backup = _sessions.Select(e => e.Clone()).ToList();
        var result = await _editor.DeleteAsync([.. rows.SelectMany(r => r.Sessions)], _sessions);

        switch (result.Status)
        {
            case EntryEditStatus.Declined:
                return false;

            case EntryEditStatus.Saved:
                Status = TrackerStatus.Success;
                StatusText = $"✓ Deleted {summary}";
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
    public async Task<bool> ReplaceSessionsAsync(string task, EditEntriesViewModel editor)
    {
        var backup = _sessions.Select(e => e.Clone()).ToList();
        var result = await _editor.ReplaceSessionsAsync(task, editor.RemainingSessions(), _sessions);

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
        task = task.Trim();
        if (task.Length == 0)
        {
            InvalidTaskName?.Invoke();
            return false;
        }

        // Compare against the committed snapshot: the grid binding stages the edited
        // text in the row BEFORE this runs, so row.Task/row.BookingElement already
        // hold the new values and cannot be used to detect a change.
        if (task == row.CommittedTask && bookingElement == row.CommittedBookingElement)
        {
            // Nothing changed; drop the staged edit and restore the committed text.
            row.CommitText(row.CommittedTask, row.CommittedBookingElement);
            return true;
        }

        var result = await _editor.UpdateTextAsync(row.Sessions, task, bookingElement, _sessions);

        switch (result.Status)
        {
            case EntryEditStatus.Saved:
                row.CommitText(task, bookingElement);
                Status = TrackerStatus.Success;
                StatusText = $"✓ Updated \"{result.Summary}\"";
                await RefreshEntriesAsync();
                _history.RevealTask(result.Summary);
                return true;

            default:
                // The file was not changed; rebuild the in-memory state from it.
                await RefreshEntriesAsync();

                Status = TrackerStatus.Error;
                StatusText = "✗ Update failed: " + result.Error!.Message;
                ErrorOccurred?.Invoke("Could not save the change:\n" + result.Error.Message);
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
        _startedAt = _now();
        _watch.Restart();
        _timer.Start();
        IsRunning = true;

        ElapsedTimeText = "00:00:00";
        Status = TrackerStatus.Info;
        StatusText = $"Tracking \"{task}\" since {_startedAt:HH:mm:ss} …";
        Title = "Timetracker – " + task;

        // The preview is consumed with the next save (see BuildEntry).
        RefreshCommands();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task StopAsync()
    {
        if (!IsRunning)
            return;
        await HaltAndSaveAsync(_now(), _watch.Elapsed);
    }

    private async Task HaltAndSaveAsync(DateTimeOffset endedAt, TimeSpan elapsed)
    {
        _timer.Stop();
        _watch.Stop();
        IsRunning = false;
        Title = "Timetracker";

        await SaveAsync(BuildEntry(endedAt, elapsed));

        // Only now: before the save refresh the stopped session would briefly show as a gap.
        PreviewBookingElement = "";

        RefreshCommands();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private TrackerEntry BuildEntry(DateTimeOffset endedAt, TimeSpan elapsed) => new()
    {
        Task = TaskName.Trim(),
        // An integration-provided element wins for this session; otherwise the new
        // session inherits the task's latest booking element so grouping stays consistent.
        BookingElement = PreviewBookingElement.Length > 0
            ? PreviewBookingElement
            : TrackerEntry.LatestBookingElement(_sessions, TaskName),
        Start = _startedAt,
        End = endedAt,
        Duration = elapsed.ToString(@"hh\:mm\:ss"),
        DurationSeconds = Math.Round(elapsed.TotalSeconds, 1),
    };

    private async Task SaveAsync(TrackerEntry entry)
    {
        try
        {
            await _repository.AddAsync(entry);

            Status = TrackerStatus.Success;
            StatusText = $"✓ Saved {entry.Duration} to {_repository.FilePath}";

            await RefreshEntriesAsync();
            _history.RevealTask(entry.Task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save the entry of {Task}", entry.Task);
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
        _sessions = [.. await _repository.GetAllAsync()];

        // The history list groups the sessions into one row per task, applies the
        // active sort and filter, and pages the result.
        _history.SetSessions(_sessions);

        RefreshSuggestions();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSuggestions() =>
        _suggestions.Refresh(TaskName, _sessions.Select(e => (e.Task, e.DurationSeconds)));

    private void OnTimerTick()
    {
        if (!_watch.IsRunning)
        {
            return;
        }

        ElapsedTimeText = _watch.Elapsed.ToString(@"hh\:mm\:ss");
    }

    private void RefreshCommands()
    {
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
    }
}
