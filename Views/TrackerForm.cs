using System.ComponentModel;
using Timetracker.Services;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Thin view: builds the controls, binds them to the view models and forwards user
/// interaction. Contains no business logic. Dock-based layout so it stays correct
/// on any window size (absolute positions + anchors break when a container resizes
/// before its children were laid out).
/// </summary>
public sealed class TrackerForm : Form
{
    private readonly TrackerViewModel _vm;

    private readonly TextBox _taskBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _elapsedLabel = new();
    private readonly Label _statusLabel = new();
    private readonly DataGridView _grid = new();
    private readonly ListBox _suggestionList = new();
    private readonly TabControl _tabs = new();
    private readonly TabPage _trackerTab = new("Tracker");
    private readonly TabPage _weekTab = new("Week view");

    // Week view controls
    private readonly Label _weekTitleLabel = new();
    private readonly Label _weekTotalLabel = new();
    private readonly DataGridView _weekGrid = new();
    private readonly Button _prevWeekButton = new();
    private readonly Button _nextWeekButton = new();
    private readonly Button _currentWeekButton = new();

    /// <summary>TableLayoutPanel row of the suggestion list; its height toggles with visibility.</summary>
    private RowStyle _suggestionRowStyle = null!;

    /// <summary>Suppresses suggestion handling while a suggestion is being applied.</summary>
    private bool _pickingSuggestion;

    /// <summary>Header captions without sort indicator, keyed by column property name.</summary>
    private Dictionary<string, string> _columnBaseNames = new();

    public TrackerForm(TrackerViewModel viewModel)
    {
        _vm = viewModel;

        _vm.InvalidTaskName += OnInvalidTaskName;
        _vm.ErrorOccurred += OnErrorOccurred;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        BuildUi();
        BindViewModel();
    }

    private void BuildUi()
    {
        Text = "Timetracker";
        Font = new Font("Segoe UI", 9.75F);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 560);

        _tabs.Dock = DockStyle.Fill;

        BuildTrackerTab();
        BuildWeekTab();

        _tabs.TabPages.Add(_trackerTab);
        _tabs.TabPages.Add(_weekTab);

        Controls.Add(_tabs);
    }

    private void BuildTrackerTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            ColumnCount = 1,
            RowCount = 6,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 0 label
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 1 task box
        _suggestionRowStyle = new RowStyle(SizeType.Absolute, 0);              // 2 suggestions (hidden)
        layout.RowStyles.Add(_suggestionRowStyle);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 3 buttons
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // 4 status
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));             // 5 grid

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

        ConfigureHistoryGrid(_grid);
        BuildHistoryColumns();

        // Dock order matters: the fill control is added first, the top bands after.
        layout.Controls.Add(_grid, 0, 5);
        layout.Controls.Add(_statusLabel, 0, 4);
        layout.Controls.Add(buttonRow, 0, 3);
        layout.Controls.Add(_suggestionList, 0, 2);
        layout.Controls.Add(_taskBox, 0, 1);
        layout.Controls.Add(taskLabel, 0, 0);

        _trackerTab.Controls.Add(layout);
        AcceptButton = _startButton;
    }

    private void BuildWeekTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 0 header row
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 1 buttons
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));              // 2 grid

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _weekTitleLabel.Text = "";
        _weekTitleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _weekTitleLabel.AutoSize = true;
        _weekTitleLabel.Dock = DockStyle.Fill;
        _weekTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _weekTitleLabel.Margin = new Padding(0, 0, 8, 6);

        _weekTotalLabel.Text = "";
        _weekTotalLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _weekTotalLabel.AutoSize = true;
        _weekTotalLabel.TextAlign = ContentAlignment.MiddleRight;
        _weekTotalLabel.Margin = new Padding(0, 2, 0, 6);

        headerRow.Controls.Add(_weekTitleLabel, 0, 0);
        headerRow.Controls.Add(_weekTotalLabel, 1, 0);

        var buttonRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6),
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _prevWeekButton.Text = "◀ Previous week";
        _prevWeekButton.AutoSize = true;
        _prevWeekButton.MinimumSize = new Size(130, 30);
        _prevWeekButton.Margin = new Padding(0, 0, 8, 0);

        _nextWeekButton.Text = "Next week ▶";
        _nextWeekButton.AutoSize = true;
        _nextWeekButton.MinimumSize = new Size(130, 30);
        _nextWeekButton.Margin = new Padding(0, 0, 8, 0);

        _currentWeekButton.Text = "● Current week";
        _currentWeekButton.AutoSize = true;
        _currentWeekButton.MinimumSize = new Size(130, 30);
        _currentWeekButton.Margin = new Padding(0, 0, 0, 0);

        buttonRow.Controls.Add(_prevWeekButton, 0, 0);
        buttonRow.Controls.Add(_nextWeekButton, 1, 0);
        buttonRow.Controls.Add(_currentWeekButton, 2, 0);

        _weekGrid.Dock = DockStyle.Fill;
        _weekGrid.ReadOnly = true;
        _weekGrid.AllowUserToAddRows = false;
        _weekGrid.AllowUserToDeleteRows = false;
        _weekGrid.AllowUserToResizeRows = false;
        _weekGrid.RowHeadersVisible = false;
        _weekGrid.ColumnHeadersVisible = true;
        _weekGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _weekGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _weekGrid.MultiSelect = false;
        _weekGrid.AutoGenerateColumns = false;
        _weekGrid.BackgroundColor = SystemColors.Window;
        _weekGrid.BorderStyle = BorderStyle.None;
        _weekGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Grow rows with wrapped content, so days with several bookings show all of them.
        _weekGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _weekGrid.Margin = new Padding(0);

        // One column per weekday (Monday first); fill the available width evenly.
        for (var i = 0; i < 7; i++)
        {
            var column = new DataGridViewTextBoxColumn
            {
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 1,
            };
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _weekGrid.Columns.Add(column);
        }

        // Dock order matters: the fill control is added first, the top bands after.
        layout.Controls.Add(_weekGrid, 0, 2);
        layout.Controls.Add(buttonRow, 0, 1);
        layout.Controls.Add(headerRow, 0, 0);

        _weekTab.Controls.Add(layout);

        _prevWeekButton.Click += (_, _) => _vm.Week.PreviousWeekCommand.Execute(null);
        _nextWeekButton.Click += (_, _) => _vm.Week.NextWeekCommand.Execute(null);
        _currentWeekButton.Click += (_, _) => _vm.Week.CurrentWeekCommand.Execute(null);

        _vm.Week.PropertyChanged += OnWeekPropertyChanged;
        _vm.Week.Days.ListChanged += (_, _) => UpdateWeekGrid();
        UpdateWeekGrid();
    }

    private static void ConfigureHistoryGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = false;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
    }

    private void BuildHistoryColumns()
    {
        var taskColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.Task),
            HeaderText = "Task",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 28,
            SortMode = DataGridViewColumnSortMode.Programmatic,
        };

        var descriptionColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(EntryRow.Description),
            HeaderText = "Description",
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

        _grid.Columns.AddRange(taskColumn, descriptionColumn, startedColumn, endedColumn, durationColumn);

        // Only Task and Description are editable; started/ended/duration stay read-only.
        taskColumn.ReadOnly = false;
        descriptionColumn.ReadOnly = false;
        startedColumn.ReadOnly = true;
        endedColumn.ReadOnly = true;
        durationColumn.ReadOnly = true;
    }

    private void OnWeekPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeekViewModel.WeekTitle))
        {
            _weekTitleLabel.Text = _vm.Week.WeekTitle;
        }
        else if (e.PropertyName == nameof(WeekViewModel.WeekTotalText))
        {
            _weekTotalLabel.Text = _vm.Week.WeekTotalText;
        }
    }

    private void UpdateWeekGrid()
    {
        _weekTitleLabel.Text = _vm.Week.WeekTitle;
        _weekTotalLabel.Text = _vm.Week.WeekTotalText;

        // Single row whose cells hold the per-day bookings; column headers show the dates.
        if (_weekGrid.Rows.Count == 0)
        {
            _weekGrid.Rows.Add();
        }

        for (var i = 0; i < 7; i++)
        {
            var day = _vm.Week.Days[i];
            _weekGrid.Columns[i].HeaderText = day.Header;
            _weekGrid.Rows[0].Cells[i].Value = day.EntriesText;

            // Highlight the today column.
            _weekGrid.Columns[i].DefaultCellStyle.BackColor = day.IsToday
                ? Color.Moccasin
                : SystemColors.Window;
        }

        // No cell should look pre-selected when the tab opens.
        _weekGrid.ClearSelection();
    }

    private void BindViewModel()
    {
        // Base header captions, so the sort indicator can be added and removed again.
        _columnBaseNames = _grid.Columns.Cast<DataGridViewColumn>()
            .ToDictionary(c => c.DataPropertyName, c => c.HeaderText);

        _taskBox.DataBindings.Add(nameof(TextBox.Text), _vm, nameof(_vm.TaskName),
            formattingEnabled: false, DataSourceUpdateMode.OnPropertyChanged);
        _taskBox.DataBindings.Add(nameof(TextBox.ReadOnly), _vm, nameof(_vm.IsRunning));

        _elapsedLabel.DataBindings.Add(nameof(Label.Text), _vm, nameof(_vm.ElapsedTimeText));
        _statusLabel.DataBindings.Add(nameof(Label.Text), _vm, nameof(_vm.StatusText));
        DataBindings.Add(nameof(Text), _vm, nameof(_vm.Title));

        _grid.DataSource = _vm.Entries;
        _suggestionList.DataSource = _vm.Suggestions;
        _suggestionList.DisplayMember = nameof(SuggestionItem.DisplayText);

        _taskBox.TextChanged += OnTaskBoxTextChanged;
        _taskBox.KeyDown += OnTaskBoxKeyDown;

        _grid.ColumnHeaderMouseClick += OnGridColumnHeaderClick;
        _grid.CellValidating += OnGridCellValidating;
        _grid.CellEndEdit += OnGridCellEndEdit;
        _grid.DataError += OnGridDataError;

        CommandBindings.Bind(_startButton, _vm.StartCommand);
        CommandBindings.Bind(_stopButton, _vm.StopCommand);

        UpdateSortGlyphs();
        UpdateSuggestions();
    }

    private void OnTaskBoxTextChanged(object? sender, EventArgs e)
    {
        if (_pickingSuggestion) return;
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
        if (_suggestionList.SelectedItem is not SuggestionItem suggestion) return;

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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackerViewModel.IsRunning))
        {
            // Enter starts when idle and stops while running; keep focus on the active button.
            AcceptButton = _vm.IsRunning ? _stopButton : _startButton;
        }
        else if (e.PropertyName == nameof(TrackerViewModel.Suggestions))
        {
            UpdateSuggestions();
        }
        else if (e.PropertyName is nameof(TrackerViewModel.SortColumn) or nameof(TrackerViewModel.SortAscending))
        {
            UpdateSortGlyphs();
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
            // WinForms forbids sort glyphs on NotSortable columns (e.g. Description);
            // touching them would throw an InvalidOperationException.
            if (column.SortMode == DataGridViewColumnSortMode.NotSortable) continue;

            if (!_columnBaseNames.TryGetValue(column.DataPropertyName, out var baseName)) continue;

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
        if (columnName is not (nameof(EntryRow.Task) or nameof(EntryRow.Description))) return;

        if (e.RowIndex < 0 || e.RowIndex >= _vm.Entries.Count)
        {
            e.Cancel = true;
            return;
        }

        // Reject empty task names up front (descriptions may be empty).
        if (columnName == nameof(EntryRow.Task) && string.IsNullOrWhiteSpace(e.FormattedValue?.ToString()))
        {
            e.Cancel = true;
        }
    }

    private void OnGridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        var columnName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (columnName is not (nameof(EntryRow.Task) or nameof(EntryRow.Description))) return;

        if (e.RowIndex < 0 || e.RowIndex >= _vm.Entries.Count) return;
        var row = _vm.Entries[e.RowIndex];

        // The binding has pushed the new text into EntryRow by now. Commit deferred,
        // so a re-sort inside the view model cannot reenter the grid event pipeline.
        BeginInvoke(() => _vm.UpdateEntryText(row, row.Task, row.Description));
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

    private void OnErrorOccurred(string message)
    {
        MessageBox.Show(this, message, "Timetracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Never lose a running entry: the view model saves it before the window closes.
        _vm.SaveRunningEntryOnClose();
        base.OnFormClosing(e);
    }
}