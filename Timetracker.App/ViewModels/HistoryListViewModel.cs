using System.Collections.ObjectModel;
using System.Windows.Input;
using Timetracker.App.Models;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;

namespace Timetracker.App.ViewModels;

/// <summary>
/// History list state: groups the saved sessions into one row per task, and owns
/// the sorting, per-column filtering, paging and row-reveal behavior over those
/// rows. Kept separate from <see cref="TrackerViewModel"/> so the list concern can
/// be tested on its own; the tracker forwards its own bindings to this.
/// <para>
/// The list is fed by <see cref="SetSessions"/> and is the source of truth for the
/// rows shown in the grid; it never writes to the repository.
/// </para>
/// </summary>
public sealed class HistoryListViewModel : ObservableObject
{
    /// <summary>Rows shown in the history grid per page; paging appears above this size.</summary>
    private const int PageSize = 10;

    private readonly ObservableCollection<EntryRow> _entries = new();
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;

    /// <summary>Every session the list was last fed, in file order.</summary>
    private IReadOnlyList<TrackerEntry> _sessions = [];

    /// <summary>All rows in sort order, before filtering; the visible page is drawn from the filtered set.</summary>
    private List<EntryRow> _allRows = [];

    /// <summary>The rows that pass the current filter, in sort order; paging runs over these.</summary>
    private List<EntryRow> _visibleRows = [];

    private string _sortColumn = nameof(EntryRow.StartText);
    private bool _sortAscending;
    private int _currentPage = 1;
    private int _totalPages = 1;

    /// <summary>
    /// Per-column filters, keyed by the <see cref="EntryRow"/> property they apply
    /// to (Task, BookingElement, StartText, EndText). A row must match every filled
    /// filter; an absent key means that column is unfiltered.
    /// </summary>
    private readonly Dictionary<string, string> _columnFilters = new(StringComparer.Ordinal);

    public HistoryListViewModel()
    {
        _previousPageCommand = new RelayCommand(() => GoToPage(CurrentPage - 1), () => CurrentPage > 1);
        _nextPageCommand = new RelayCommand(() => GoToPage(CurrentPage + 1), () => CurrentPage < TotalPages);
    }

    /// <summary>Tasks (grouped sessions) of the current history page.</summary>
    public ObservableCollection<EntryRow> Entries => _entries;

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

    /// <summary>True while any column filter is set; the view highlights active funnels.</summary>
    public bool IsFilterActive => _columnFilters.Count > 0;

    /// <summary>True when the given column currently has a filter.</summary>
    public bool IsColumnFiltered(string column) => _columnFilters.ContainsKey(column);

    /// <summary>
    /// Rebuilds the rows from a new session log, keeping the current sort and
    /// filters. One row per distinct task name, durations summed across its
    /// sessions, ordered by the active sort.
    /// </summary>
    public void SetSessions(IReadOnlyList<TrackerEntry> sessions)
    {
        _sessions = sessions;

        var rows = sessions
            .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new EntryRow([.. g]))
            .ToList();
        rows.Sort(CompareRows);
        _allRows = rows;
        ApplyFilter();
    }

    /// <summary>
    /// Sets the filter for one column (by the <see cref="EntryRow"/> property name)
    /// and refreshes the list. Empty clears that column's filter. Filtering spans all
    /// pages, like sorting; a row must match every filled column filter.
    /// </summary>
    public void SetColumnFilter(string column, string filter)
    {
        filter = filter.Trim();
        var changed = filter.Length == 0
            ? _columnFilters.Remove(column)
            : SetOrUpdate(column, filter);

        if (changed)
        {
            OnPropertyChanged(nameof(IsFilterActive));
            ApplyFilter();
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

    /// <summary>
    /// Flips to the page containing the row for the given task, so the row the user
    /// just saved or edited stays visible even if its sort position is on another page.
    /// Clears a filter that would hide the row, so the reveal always succeeds.
    /// </summary>
    public void RevealTask(string task)
    {
        var row = _allRows.FirstOrDefault(r =>
            r.Task.Equals(task.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (row is null)
        {
            return;
        }

        // A filter that excludes the row would make it unreachable; drop them all.
        if (!_visibleRows.Contains(row))
        {
            _columnFilters.Clear();
            OnPropertyChanged(nameof(IsFilterActive));
            ApplyFilter();
        }

        var index = _visibleRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var page = index / PageSize + 1;
        if (page != CurrentPage)
        {
            CurrentPage = page;
            FillEntries();
        }
    }

    private bool SetOrUpdate(string column, string filter)
    {
        if (_columnFilters.TryGetValue(column, out var existing) && existing == filter)
        {
            return false;
        }

        _columnFilters[column] = filter;
        return true;
    }

    private void SortEntries()
    {
        var rows = _allRows.ToList();
        rows.Sort(CompareRows);
        _allRows = rows;
        ApplyFilter();
    }

    /// <summary>
    /// Recomputes the visible rows from the column filters and shows the first page
    /// of them. Called whenever the rows or a filter change.
    /// </summary>
    private void ApplyFilter()
    {
        _visibleRows = _columnFilters.Count == 0
            ? [.. _allRows]
            : [.. _allRows.Where(MatchesFilters)];

        CurrentPage = 1;
        FillEntries();
    }

    /// <summary>A row passes when every filled column filter matches its column's text.</summary>
    private bool MatchesFilters(EntryRow row) =>
        _columnFilters.All(filter => ColumnValue(row, filter.Key)
            .Contains(filter.Value, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>The text a filter on the given column compares against.</summary>
    private static string ColumnValue(EntryRow row, string column) => column switch
    {
        nameof(EntryRow.Task) => row.Task,
        nameof(EntryRow.BookingElement) => row.BookingElement,
        nameof(EntryRow.StartText) => row.StartText,
        nameof(EntryRow.EndText) => row.EndText,
        _ => "",
    };

    /// <summary>Shows the current page of the visible (filtered) rows.</summary>
    private void FillEntries()
    {
        TotalPages = Math.Max(1, (_visibleRows.Count + PageSize - 1) / PageSize);
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        var pageRows = _visibleRows
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // One reset instead of a change event per row, so the grid redraws in one go.
        Entries.Clear();
        foreach (var row in pageRows)
        {
            Entries.Add(row);
        }

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
        FillEntries();
    }

    private void RefreshPageCommands()
    {
        _previousPageCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
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
}
