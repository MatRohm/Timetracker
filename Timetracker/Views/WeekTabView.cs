using System.ComponentModel;
using Timetracker.ActivityMonitor;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Week view tab: one column per weekday (Monday first) showing all booked times,
/// with navigation to previous/next/current week. View-only; state lives in
/// <see cref="WeekViewModel"/>.
/// </summary>
public sealed class WeekTabView : UserControl
{
    private readonly WeekViewModel _week;

    private readonly Label _titleLabel = new();
    private readonly Label _totalLabel = new();
    private readonly DataGridView _grid = new();
    private readonly Button _prevButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _currentButton = new();
    private readonly CheckBox _groupByBookingElementCheck = new();
    private readonly Button _installMonitorButton = new();
    private readonly Button _uninstallMonitorButton = new();
    private readonly Label _monitorStateLabel = new();

    public WeekTabView(WeekViewModel week)
    {
        _week = week;
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
            RowCount = 4,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 0 header row
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 1 buttons
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                  // 2 monitor row
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));              // 3 grid

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
            ColumnCount = 3,
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

        var monitorRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
        };
        monitorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        monitorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        monitorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        monitorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _installMonitorButton.Text = "⏻ Install PC activity monitor";
        _installMonitorButton.AutoSize = true;
        _installMonitorButton.MinimumSize = new Size(200, 30);
        _installMonitorButton.Margin = new Padding(0, 0, 8, 6);

        _uninstallMonitorButton.Text = "⏻ Remove PC activity monitor";
        _uninstallMonitorButton.AutoSize = true;
        _uninstallMonitorButton.MinimumSize = new Size(210, 30);
        _uninstallMonitorButton.Margin = new Padding(0, 0, 8, 6);

        _monitorStateLabel.AutoSize = true;
        _monitorStateLabel.ForeColor = Color.DimGray;
        _monitorStateLabel.Dock = DockStyle.Fill;
        _monitorStateLabel.TextAlign = ContentAlignment.MiddleLeft;
        _monitorStateLabel.Margin = new Padding(0, 6, 0, 6);

        monitorRow.Controls.Add(_installMonitorButton, 0, 0);
        monitorRow.Controls.Add(_uninstallMonitorButton, 1, 0);
        monitorRow.Controls.Add(_monitorStateLabel, 2, 0);

        _installMonitorButton.Click += (_, _) =>
        {
            var message = ActivityMonitorInstaller.Install()
                ? "The PC activity monitor will start with Windows."
                : "Installation failed. Make sure \"" + ActivityMonitorInstaller.MonitorExePath
                    + "\" exists next to the app.";
            MessageBox.Show(this, message, "PC activity monitor",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateMonitorButtons();
        };

        _uninstallMonitorButton.Click += (_, _) =>
        {
            var message = ActivityMonitorInstaller.Uninstall()
                ? "The PC activity monitor autostart was removed."
                : "Removing the autostart entry failed.";
            MessageBox.Show(this, message, "PC activity monitor",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateMonitorButtons();
        };

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
        layout.Controls.Add(_grid, 0, 3);
        layout.Controls.Add(monitorRow, 0, 2);
        layout.Controls.Add(buttonRow, 0, 1);
        layout.Controls.Add(headerRow, 0, 0);

        Controls.Add(layout);
    }

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
        UpdateMonitorButtons();
    }

    /// <summary>Enables exactly the button that matches the autostart state.</summary>
    private void UpdateMonitorButtons()
    {
        var installed = ActivityMonitorInstaller.IsInstalled;
        _installMonitorButton.Enabled = !installed;
        _uninstallMonitorButton.Enabled = installed;
        _monitorStateLabel.Text = installed
            ? "Monitoring is installed (starts with Windows)."
            : "The monitor is not installed; PC activity is only recorded while it runs.";
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

        // Two rows: bookings per day on top, the PC activity line below.
        if (_grid.Rows.Count == 0)
        {
            _grid.Rows.Add();
            _grid.Rows.Add();
            _grid.Rows[1].ReadOnly = true;
        }

        for (var i = 0; i < 7; i++)
        {
            var day = _week.Days[i];
            _grid.Columns[i].HeaderText = day.Header;
            _grid.Rows[0].Cells[i].Value = day.EntriesText;
            _grid.Rows[1].Cells[i].Value = day.ActivityText;

            // Highlight the today column.
            _grid.Columns[i].DefaultCellStyle.BackColor = day.IsToday
                ? Color.Moccasin
                : SystemColors.Window;
        }

        // Style the activity row as a compact summary line.
        _grid.Rows[1].DefaultCellStyle.ForeColor = Color.DimGray;
        _grid.Rows[1].DefaultCellStyle.Font = new Font(
            _grid.Font, FontStyle.Italic);
        _grid.Rows[1].Height = 24;

        // No cell should look pre-selected when the tab opens.
        _grid.ClearSelection();
    }
}
