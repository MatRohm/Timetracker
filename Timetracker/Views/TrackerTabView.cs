using System.ComponentModel;
using Timetracker.AzureDevOps;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Tracker tab: task input with suggestions, start/stop controls, status line and
/// the sortable, inline-editable history grid. View-only; logic lives in
/// <see cref="TrackerViewModel"/>.
/// </summary>
public sealed class TrackerTabView : UserControl, ITrackerUiHost
{
    private readonly TrackerViewModel _vm;

    private readonly TextBox _taskBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _elapsedLabel = new();
    private readonly Label _statusLabel = new();
    private readonly DataGridView _grid = new();
    private readonly ListBox _suggestionList = new();
    private readonly Button _previousPageButton = new();
    private readonly Label _pageLabel = new();
    private readonly Button _nextPageButton = new();

    /// <summary>Pager band below the grid; only visible when the rows span several pages.</summary>
    private TableLayoutPanel _pagerRow = null!;

    /// <summary>Layout row of the suggestion list; its height toggles with visibility.</summary>
    private RowStyle _suggestionRowStyle = null!;

    /// <summary>Suppresses suggestion handling while a suggestion is being applied.</summary>
    private bool _pickingSuggestion;

    /// <summary>Header captions without sort indicator, keyed by column property name.</summary>
    private Dictionary<string, string> _columnBaseNames = new();

    /// <summary>Raised when IsRunning changed, so the shell can update the Enter button.</summary>
    public event Action? RunningStateChanged;

    public Button StartButton => _startButton;

    public Button StopButton => _stopButton;

    public TrackerTabView(TrackerViewModel viewModel)
    {
        _vm = viewModel;
        Dock = DockStyle.Fill;

        BuildUi();
        BindViewModel();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            ColumnCount = 1,
            RowCount = 8,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 0 label
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 1 task box
        _suggestionRowStyle = new RowStyle(SizeType.Absolute, 0);              // 2 suggestions (hidden)
        layout.RowStyles.Add(_suggestionRowStyle);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 3 buttons
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 4 status
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 5 azure devops
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));             // 6 grid
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 7 pager

        var taskLabel = new Label
        {
            Text = "Task name",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };

        _taskBox.Dock = DockStyle.Top;
        _taskBox.Margin = new Padding(0, 0, 0, 4);

        _suggestionList.Dock = DockStyle.Fill;
        _suggestionList.BorderStyle = BorderStyle.FixedSingle;
        _suggestionList.IntegralHeight = false;
        _suggestionList.Visible = false;
        _suggestionList.Margin = new Padding(0);
        _suggestionList.DoubleClick += OnSuggestionChosen;
        _suggestionList.KeyDown += OnSuggestionKeyDown;

        var buttonRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _startButton.Text = "▶ Start";
        _startButton.AutoSize = true;
        _startButton.MinimumSize = new Size(110, 36);
        _startButton.Margin = new Padding(0, 0, 8, 4);

        _stopButton.Text = "■ Stop";
        _stopButton.AutoSize = true;
        _stopButton.MinimumSize = new Size(110, 36);
        _stopButton.Margin = new Padding(0, 0, 8, 4);

        _elapsedLabel.Text = "00:00:00";
        _elapsedLabel.Font = new Font("Consolas", 15.75F, FontStyle.Bold);
        _elapsedLabel.AutoSize = true;
        _elapsedLabel.TextAlign = ContentAlignment.MiddleRight;
        _elapsedLabel.Dock = DockStyle.Fill;
        _elapsedLabel.Margin = new Padding(0, 0, 0, 4);

        buttonRow.Controls.Add(_startButton, 0, 0);
        buttonRow.Controls.Add(_stopButton, 1, 0);
        buttonRow.Controls.Add(_elapsedLabel, 2, 0);

        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.AutoSize = true;
        _statusLabel.Margin = new Padding(0, 2, 0, 4);

        var pagerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
        };
        pagerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pagerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pagerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _previousPageButton.Text = "◀ Previous";
        _previousPageButton.AutoSize = true;
        _previousPageButton.MinimumSize = new Size(110, 28);
        _previousPageButton.Margin = new Padding(0, 4, 8, 0);

        _pageLabel.AutoSize = true;
        _pageLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pageLabel.Margin = new Padding(0, 9, 8, 0);

        _nextPageButton.Text = "Next ▶";
        _nextPageButton.AutoSize = true;
        _nextPageButton.MinimumSize = new Size(110, 28);
        _nextPageButton.Margin = new Padding(0, 4, 0, 0);

        pagerRow.Controls.Add(_previousPageButton, 0, 0);
        pagerRow.Controls.Add(_pageLabel, 1, 0);
        pagerRow.Controls.Add(_nextPageButton, 2, 0);
        _pagerRow = pagerRow;

        ConfigureGrid();
        BuildColumns();

        var azureDevOpsPanel = new AzureDevOpsPanel(
            new AzureDevOpsService(), this);

        // Dock order matters: the fill control is added first, the top bands after.
        layout.Controls.Add(_pagerRow, 0, 7);
        layout.Controls.Add(_grid, 0, 6);
        layout.Controls.Add(azureDevOpsPanel, 0, 5);
        layout.Controls.Add(_statusLabel, 0, 4);
        layout.Controls.Add(buttonRow, 0, 3);
        layout.Controls.Add(_suggestionList, 0, 2);
        layout.Controls.Add(_taskBox, 0, 1);
        layout.Controls.Add(taskLabel, 0, 0);

        Controls.Add(layout);
    }

    private void BindViewModel()
    {
        // Base header captions, so the sort indicator can be added and removed again.
        _columnBaseNames = _grid.Columns.Cast<DataGridViewColumn>()
            .ToDictionary(c => c.DataPropertyName, c => c.HeaderText);

        _taskBox.DataBindings.Add(nameof(TextBox.Text), _vm, nameof(TrackerViewModel.TaskName),
            formattingEnabled: false, DataSourceUpdateMode.OnPropertyChanged);
        _taskBox.DataBindings.Add(nameof(TextBox.ReadOnly), _vm, nameof(TrackerViewModel.IsRunning));

        _elapsedLabel.DataBindings.Add(nameof(Label.Text), _vm, nameof(TrackerViewModel.ElapsedTimeText));
        _statusLabel.DataBindings.Add(nameof(Label.Text), _vm, nameof(TrackerViewModel.StatusText));

        _grid.DataSource = _vm.Entries;
        _suggestionList.DataSource = _vm.Suggestions;
        _suggestionList.DisplayMember = nameof(SuggestionItem.DisplayText);

        _pageLabel.DataBindings.Add(nameof(Label.Text), _vm, nameof(TrackerViewModel.PageText),
            formattingEnabled: false);

        _taskBox.TextChanged += OnTaskBoxTextChanged;
        _taskBox.KeyDown += OnTaskBoxKeyDown;

        _grid.ColumnHeaderMouseClick += OnGridColumnHeaderClick;
        _grid.CellDoubleClick += OnGridCellDoubleClick;
        _grid.KeyDown += OnGridKeyDown;
        _grid.CellValidating += OnGridCellValidating;
        _grid.CellEndEdit += OnGridCellEndEdit;
        _grid.DataError += OnGridDataError;

        CommandBindings.Bind(_startButton, _vm.StartCommand);
        CommandBindings.Bind(_stopButton, _vm.StopCommand);
        CommandBindings.Bind(_previousPageButton, _vm.PreviousPageCommand);
        CommandBindings.Bind(_nextPageButton, _vm.NextPageCommand);

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.InvalidTaskName += OnInvalidTaskName;

        UpdateSortGlyphs();
        UpdateSuggestions();
        UpdatePager();
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        // Row-based multi-selection: click selects a row, SHIFT+CLICK selects the
        // range from the anchor row, CTRL+CLICK adds or removes single rows. Del
        // then deletes every selected row together.
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
    }

    private void BuildColumns()
    {
        var taskColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.Task),
            HeaderText = "Task",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 28,
            SortMode = DataGridViewColumnSortMode.Programmatic,
        };

        var bookingElementColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.BookingElement),
            HeaderText = "Booking element",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 28,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };

        var startedColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.StartText),
            HeaderText = "Started",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 18,
            SortMode = DataGridViewColumnSortMode.Programmatic,
        };

        var endedColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.EndText),
            HeaderText = "Ended",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 18,
            SortMode = DataGridViewColumnSortMode.Programmatic,
        };

        var durationColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.Duration),
            HeaderText = "Duration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 8,
            MinimumWidth = 80,
            SortMode = DataGridViewColumnSortMode.Programmatic,
        };
        durationColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        _grid.Columns.AddRange(taskColumn, bookingElementColumn, startedColumn, endedColumn, durationColumn);

        // Only Task and BookingElement are editable; started/ended/duration stay read-only.
        taskColumn.ReadOnly = false;
        bookingElementColumn.ReadOnly = false;
        startedColumn.ReadOnly = true;
        endedColumn.ReadOnly = true;
        durationColumn.ReadOnly = true;
    }

    private void OnTaskBoxTextChanged(object? sender, EventArgs e)
    {
        if (_pickingSuggestion)
            return;
        UpdateSuggestions();
    }

    private void OnTaskBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_suggestionList.Visible && _suggestionList.Items.Count > 0)
        {
            if (e.KeyCode == Keys.Down)
            {
                // Hand focus to the list so Arrow/Enter can pick a suggestion.
                _suggestionList.Focus();
                _suggestionList.SelectedIndex = 0;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideSuggestions();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
    }

    private void OnSuggestionChosen(object? sender, EventArgs e) => ApplySelectedSuggestion();

    private void OnSuggestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            ApplySelectedSuggestion();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            HideSuggestions();
            _taskBox.Focus();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ApplySelectedSuggestion()
    {
        if (_suggestionList.SelectedItem is not SuggestionItem suggestion)
            return;

        _pickingSuggestion = true;
        try
        {
            _vm.AcceptSuggestion(suggestion);
            HideSuggestions();
            _startButton.Focus();
        }
        finally
        {
            _pickingSuggestion = false;
        }

        UpdateSuggestions();
    }

    private void UpdateSuggestions()
    {
        var show = !_pickingSuggestion && _vm.Suggestions.Count > 0;
        _suggestionList.Visible = show;
        _suggestionRowStyle.Height = show ? 100 : 0;
    }

    private void UpdatePager()
    {
        // Paging only appears when there is more than one page of rows.
        _pagerRow.Visible = _vm.HasMultiplePages;
    }

    private void HideSuggestions()
    {
        _suggestionList.Visible = false;
        _suggestionRowStyle.Height = 0;
    }

    private void OnGridColumnHeaderClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex >= 0)
        {
            _vm.ApplySort(_grid.Columns[e.ColumnIndex].DataPropertyName);
        }
    }

    /// <summary>Double-clicking a row starts the timer for that task.</summary>
    private void OnGridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        // Ignore header clicks; a running timer is left untouched.
        if (e.RowIndex < 0 || e.RowIndex >= _vm.Entries.Count)
            return;

        _vm.StartFromRow(_vm.Entries[e.RowIndex]);
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        // Edit mode now lives on F2 only (double-click starts the timer instead).
        if (e.KeyCode == Keys.F2
            && _grid.CurrentCell is { } cell
            && _grid.Columns[cell.ColumnIndex].ReadOnly == false)
        {
            _grid.BeginEdit(true);
            e.Handled = true;
            return;
        }

        // Delete removes the selected entries (with confirmation).
        if (e.KeyCode == Keys.Delete && !_grid.IsCurrentCellInEditMode)
        {
            DeleteSelectedEntries();
            e.Handled = true;
        }
    }

    /// <summary>Rows currently selected in the grid (distinct, in list order).</summary>
    private List<EntryRow> SelectedRows() => _grid.SelectedCells.Cast<DataGridViewCell>()
        .Where(c => c.RowIndex >= 0 && c.RowIndex < _vm.Entries.Count)
        .Select(c => _vm.Entries[c.RowIndex])
        .Distinct()
        .ToList();

    private void DeleteSelectedEntries()
    {
        var rows = SelectedRows();
        if (rows.Count == 0)
        {
            return;
        }

        _vm.DeleteEntries(rows, ConfirmDelete);
    }

    private bool ConfirmDelete(string summary) => MessageBox.Show(this,
        $"Delete {summary}?\n\nThis removes the sessions from the JSON file and cannot be undone.",
        "Timetracker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackerViewModel.IsRunning))
        {
            RunningStateChanged?.Invoke();
        }
        else if (e.PropertyName == nameof(TrackerViewModel.Suggestions))
        {
            UpdateSuggestions();
        }
        else if (e.PropertyName is nameof(TrackerViewModel.SortColumn) or nameof(TrackerViewModel.SortAscending))
        {
            UpdateSortGlyphs();
        }
        else if (e.PropertyName == nameof(TrackerViewModel.TotalPages))
        {
            UpdatePager();
        }
        else if (e.PropertyName == nameof(TrackerViewModel.Status))
        {
            _statusLabel.ForeColor = _vm.Status switch
            {
                TrackerStatus.Success => Color.ForestGreen,
                TrackerStatus.Error => Color.Firebrick,
                _ => Color.DimGray,
            };
        }
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            // WinForms forbids sort glyphs on NotSortable columns (e.g. BookingElement);
            // touching them would throw an InvalidOperationException.
            if (column.SortMode == DataGridViewColumnSortMode.NotSortable)
                continue;

            if (!_columnBaseNames.TryGetValue(column.DataPropertyName, out var baseName))
                continue;

            if (column.DataPropertyName == _vm.SortColumn)
            {
                // Prominent direction icon right in the header text (▲ ascending / ▼ descending).
                column.HeaderText = (_vm.SortAscending ? "▲ " : "▼ ") + baseName;
                column.HeaderCell.SortGlyphDirection =
                    _vm.SortAscending ? SortOrder.Ascending : SortOrder.Descending;
            }
            else
            {
                column.HeaderText = baseName;
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
        }
    }

    /// <summary>Rejects edits that would leave an empty task name or touch an empty row.</summary>
    private void OnGridCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        var columnName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (columnName is not (nameof(EntryRow.Task) or nameof(EntryRow.BookingElement)))
            return;

        if (e.RowIndex < 0 || e.RowIndex >= _vm.Entries.Count)
        {
            e.Cancel = true;
            return;
        }

        // Reject empty task names up front (booking elements may be empty).
        if (columnName == nameof(EntryRow.Task) && string.IsNullOrWhiteSpace(e.FormattedValue?.ToString()))
        {
            e.Cancel = true;
        }
    }

    private void OnGridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        var columnName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (columnName is not (nameof(EntryRow.Task) or nameof(EntryRow.BookingElement)))
            return;

        if (e.RowIndex < 0 || e.RowIndex >= _vm.Entries.Count)
            return;
        var row = _vm.Entries[e.RowIndex];

        // The binding has pushed the new text into EntryRow by now. Commit deferred,
        // so a re-sort inside the view model cannot reenter the grid event pipeline.
        BeginInvoke(() => _vm.UpdateEntryText(row, row.Task, row.BookingElement));
    }

    private void OnGridDataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        // Never crash on binding/format issues in editable cells; log instead.
        if (e.Exception is not null)
        {
            ErrorLog.Log("Grid binding", e.Exception);
        }
        e.ThrowException = false;
    }

    private void OnInvalidTaskName()
    {
        MessageBox.Show(this, "Please enter a task name first.", "Timetracker",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _taskBox.Focus();
    }

    void ITrackerUiHost.SetTaskName(string taskName)
    {
        // The binding pushes the value into the view model (and suggestions).
        _taskBox.Text = taskName;
    }

    void ITrackerUiHost.SetBookingElement(string bookingElement)
    {
        // Staging into the first row would persist nothing; the value is picked up
        // when the user starts tracking (BuildEntry inherits the task's element),
        // so it is shown as a status hint instead of a silent row edit.
        _vm.PreviewBookingElement = bookingElement;
    }
}
