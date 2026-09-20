using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Week view tab: one column per weekday (Monday first) showing all booked times,
/// with navigation to previous/next/current week. View-only; state lives in
/// <see cref="WeekViewModel"/>. Additional per-day lines (e.g. PC activity) and
/// setup bands come from the registered <see cref="IWeekDayContributor"/> and
/// <see cref="IUiContributor"/> hooks.
/// </summary>
public sealed class WeekTabView : UserControl, IWeekStatusHost
{
    private readonly WeekViewModel _week;
    private readonly IServiceProvider _services;
    private readonly IReadOnlyList<IWeekDayContributor> _weekDayContributors;
    private readonly IReadOnlyList<IUiContributor> _uiContributors;

    private readonly Label _titleLabel = new();
    private readonly Label _totalLabel = new();
    private readonly DataGridView _grid = new();
    private readonly Button _prevButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _currentButton = new();
    private readonly CheckBox _groupByBookingElementCheck = new();
    private readonly Label _statusLabel = new();

    /// <summary>Band row between buttons and grid where contributor controls go.</summary>
    private TableLayoutPanel _contributorRow = null!;

    public WeekTabView(WeekViewModel week, IServiceProvider services)
    {
        _week = week;
        _services = services;
        _weekDayContributors = services.GetServices<IWeekDayContributor>().ToArray();
        _uiContributors = services.GetServices<IUiContributor>()
            .Where(c => c.TargetTab == "Week view")
            .ToArray();
        Dock = DockStyle.Fill;

        // Add-in panels report results through the shared status line.
        UiHostAccessor.RegisterWeekStatusSink((message, success) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(() => ShowStatus(message, success));
            }
        });

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
            RowCount = 5,
        };
        // One fixed-width column: AutoSize would let long texts push the layout
        // wider than the window (same clipping problem as in the tracker tab).
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 0 header row
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 1 buttons
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 2 contributors
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));              // 3 grid
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 4 status

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

        _titleLabel.Text = "";
        _titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _titleLabel.AutoSize = true;
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _titleLabel.Margin = new Padding(0, 0, 8, 6);

        _totalLabel.Text = "";
        _totalLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _totalLabel.AutoSize = true;
        _totalLabel.TextAlign = ContentAlignment.MiddleRight;
        _totalLabel.Margin = new Padding(0, 2, 0, 6);

        headerRow.Controls.Add(_titleLabel, 0, 0);
        headerRow.Controls.Add(_totalLabel, 1, 0);

        var buttonRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6),
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _prevButton.Text = "◀ Previous week";
        _prevButton.AutoSize = true;
        _prevButton.MinimumSize = new Size(130, 30);
        _prevButton.Margin = new Padding(0, 0, 8, 0);

        _nextButton.Text = "Next week ▶";
        _nextButton.AutoSize = true;
        _nextButton.MinimumSize = new Size(130, 30);
        _nextButton.Margin = new Padding(0, 0, 8, 0);

        _currentButton.Text = "● Current week";
        _currentButton.AutoSize = true;
        _currentButton.MinimumSize = new Size(130, 30);

        buttonRow.Controls.Add(_prevButton, 0, 0);
        buttonRow.Controls.Add(_nextButton, 1, 0);
        buttonRow.Controls.Add(_currentButton, 2, 0);

        _groupByBookingElementCheck.Text = "Group by booking element";
        _groupByBookingElementCheck.AutoSize = true;
        _groupByBookingElementCheck.Checked = true;
        _groupByBookingElementCheck.Margin = new Padding(16, 4, 0, 0);

        buttonRow.Controls.Add(_groupByBookingElementCheck, 3, 0);

        _contributorRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = _uiContributors.Count + 1,
            RowCount = 1,
            Margin = new Padding(0),
        };
        for (var i = 0; i < _uiContributors.Count; i++)
        {
            _contributorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        _contributorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Contributor controls first (left to right), the filler column last.
        for (var i = 0; i < _uiContributors.Count; i++)
        {
            _contributorRow.Controls.Add(_uiContributors[i].CreateControl(_services), i, 0);
        }

        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.ForeColor = Color.DimGray;
        // Fixed height instead of AutoSize: a long text must not widen the
        // layout column (AutoEllipsis shows "..." instead).
        _statusLabel.AutoSize = false;
        _statusLabel.Height = 22;
        _statusLabel.Margin = new Padding(0, 4, 0, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.ColumnHeadersVisible = true;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Grow rows with wrapped content, so days with several bookings show all of them.
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.Margin = new Padding(0);

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
            _grid.Columns.Add(column);
        }

        // Dock order matters: the fill control is added first, the top bands after.
        layout.Controls.Add(_statusLabel, 0, 4);
        layout.Controls.Add(_grid, 0, 3);
        layout.Controls.Add(_contributorRow, 0, 2);
        layout.Controls.Add(buttonRow, 0, 1);
        layout.Controls.Add(headerRow, 0, 0);

        Controls.Add(layout);
    }

    /// <summary>One-line status at the bottom, styled like the tracker's status line.</summary>
    private void ShowStatus(string message, bool success)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = success ? Color.ForestGreen : Color.Firebrick;
    }

    void IWeekStatusHost.ShowStatus(string message, bool success) => ShowStatus(message, success);

    private void BindViewModel()
    {
        _prevButton.Click += (_, _) => _week.PreviousWeekCommand.Execute(null);
        _nextButton.Click += (_, _) => _week.NextWeekCommand.Execute(null);
        _currentButton.Click += (_, _) => _week.CurrentWeekCommand.Execute(null);

        _groupByBookingElementCheck.DataBindings.Add(nameof(CheckBox.Checked), _week,
            nameof(WeekViewModel.GroupByBookingElement), formattingEnabled: false,
            DataSourceUpdateMode.OnPropertyChanged);

        _week.PropertyChanged += OnWeekPropertyChanged;
        _week.Days.ListChanged += (_, _) => UpdateGrid();
        UpdateGrid();
    }

    private void OnWeekPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeekViewModel.WeekTitle))
        {
            _titleLabel.Text = _week.WeekTitle;
        }
        else if (e.PropertyName == nameof(WeekViewModel.WeekTotalText))
        {
            _totalLabel.Text = _week.WeekTotalText;
        }
    }

    private void UpdateGrid()
    {
        _titleLabel.Text = _week.WeekTitle;
        _totalLabel.Text = _week.WeekTotalText;

        // Header row plus one summary line per registered week-day contributor.
        if (_grid.Rows.Count == 0)
        {
            _grid.Rows.Add();
            for (var i = 0; i < _weekDayContributors.Count; i++)
            {
                _grid.Rows.Add();
                _grid.Rows[1 + i].ReadOnly = true;
            }
        }

        for (var i = 0; i < 7; i++)
        {
            var day = _week.Days[i];
            _grid.Columns[i].HeaderText = day.Header;
            _grid.Rows[0].Cells[i].Value = day.EntriesText;

            // Highlight the today column.
            _grid.Columns[i].DefaultCellStyle.BackColor = day.IsToday
                ? Color.Moccasin
                : SystemColors.Window;
        }

        // One gray summary row per contributor (e.g. PC activity).
        for (var c = 0; c < _weekDayContributors.Count; c++)
        {
            var rowIndex = 1 + c;
            for (var i = 0; i < 7; i++)
            {
                var day = _week.Days[i];
                var date = DateOnly.FromDateTime(day.Date.Date);
                _grid.Rows[rowIndex].Cells[i].Value = _weekDayContributors[c].GetDayText(date);
            }

            _grid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.DimGray;
            _grid.Rows[rowIndex].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Italic);
            _grid.Rows[rowIndex].Height = 24;
        }

        // No cell should look pre-selected when the tab opens.
        _grid.ClearSelection();
    }
}
