using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Timetracker.ViewModels;

namespace Timetracker.Views.Components;

/// <summary>
/// The sortable, inline-editable history grid with its per-row actions (open the
/// item's editor, start timing its task). Sorting, deletion and text edits are
/// delegated to <see cref="TrackerViewModel"/>; the component only renders and
/// routes the gestures. View-only.
/// </summary>
public sealed class EntryHistoryGrid : UserControl
{
    private readonly TrackerViewModel _viewModel;
    private readonly DataGrid _grid = new();

    /// <summary>Header captions without sort indicator, keyed by column property name.</summary>
    private readonly Dictionary<string, string> _columnBaseNames = new();

    /// <summary>The grid, for hosts and tests.</summary>
    public DataGrid Grid => _grid;

    public EntryHistoryGrid(TrackerViewModel viewModel)
    {
        _viewModel = viewModel;

        ConfigureGrid();
        BindViewModel();

        Content = _grid;
    }

    private void ConfigureGrid()
    {
        _grid.AutoGenerateColumns = false;
        // Sorting is applied by the view model (so the glyph and the order stay in
        // sync); the header click must still be allowed to raise Sorting.
        _grid.CanUserSortColumns = true;
        _grid.CanUserReorderColumns = false;
        // Columns can be resized by dragging the separator between headers.
        _grid.CanUserResizeColumns = true;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _grid.SelectionMode = DataGridSelectionMode.Extended;
        _grid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        _grid.BorderThickness = new Avalonia.Thickness(0);
        _grid.HorizontalAlignment = HorizontalAlignment.Stretch;
        _grid.VerticalAlignment = VerticalAlignment.Stretch;

        // Row actions: start timing this task, and open the per-item editor.
        // These stay at a fixed width, so resizing is disabled for them.
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            Width = new DataGridLength(44),
            CanUserResize = false,
            CellTemplate = new FuncDataTemplate<EntryRow>((_, _) => BuildEditButton(), true),
        });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            Width = new DataGridLength(44),
            CanUserResize = false,
            CellTemplate = new FuncDataTemplate<EntryRow>((_, _) => BuildPlayButton(), true),
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Task",
            Width = new DataGridLength(28, DataGridLengthUnitType.Star),
            SortMemberPath = nameof(EntryRow.Task),
            Binding = new Binding(nameof(EntryRow.Task)),
            IsReadOnly = false,
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Booking element",
            Width = new DataGridLength(28, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(EntryRow.BookingElement)),
            // Not sortable: the original app never sorted by booking element.
            CanUserSort = false,
            IsReadOnly = false,
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Started",
            Width = new DataGridLength(18, DataGridLengthUnitType.Star),
            SortMemberPath = nameof(EntryRow.StartText),
            Binding = new Binding(nameof(EntryRow.StartText)),
            IsReadOnly = true,
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Ended",
            Width = new DataGridLength(18, DataGridLengthUnitType.Star),
            SortMemberPath = nameof(EntryRow.EndText),
            Binding = new Binding(nameof(EntryRow.EndText)),
            IsReadOnly = true,
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Duration",
            Width = new DataGridLength(8, DataGridLengthUnitType.Star),
            MinWidth = 80,
            SortMemberPath = nameof(EntryRow.Duration),
            Binding = new Binding(nameof(EntryRow.Duration)),
            IsReadOnly = true,
        });
    }

    private void BindViewModel()
    {
        // Base header captions, so the sort indicator can be added and removed again.
        foreach (var column in _grid.Columns)
        {
            if (!string.IsNullOrEmpty(column.SortMemberPath))
            {
                _columnBaseNames[column.SortMemberPath] = column.Header?.ToString() ?? "";
            }
        }

        _grid.ItemsSource = _viewModel.Entries;
        _grid.Sorting += OnGridSorting;
        _grid.KeyDown += OnGridKeyDown;
        _grid.CellEditEnded += OnGridCellEditEnded;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateSortGlyphs();
    }

    /// <summary>
    /// Grid row action: a pencil button that opens the item's editor. The row is
    /// resolved from the button's DataContext at click time, not captured here,
    /// because the grid recycles cells and their built control across sorts.
    /// </summary>
    private Control BuildEditButton()
    {
        var button = new Button
        {
            Content = "✎",
            FontSize = 14,
            Padding = new Avalonia.Thickness(4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Edit this item's entries");

        button.Click += (_, _) =>
        {
            if (button.DataContext is EntryRow row)
            {
                OpenEditor(row);
            }
        };
        return button;
    }

    /// <summary>
    /// Grid row action: a play button that starts timing the row's task. It follows
    /// the edit button's style and is disabled while a session is already running.
    /// The row comes from the button's DataContext at click time (see
    /// <see cref="BuildEditButton"/>).
    /// </summary>
    private Control BuildPlayButton()
    {
        var button = new Button
        {
            Content = "▶",
            FontSize = 14,
            Padding = new Avalonia.Thickness(4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Start timing this task");

        button.Click += (_, _) =>
        {
            if (button.DataContext is EntryRow row)
            {
                _viewModel.StartFromRow(row);
            }
        };

        // A running session is never interrupted, so the action is unavailable then.
        button.Bind(Button.IsEnabledProperty, new Binding
        {
            Source = _viewModel,
            Path = "!" + nameof(TrackerViewModel.IsRunning),
        });
        return button;
    }

    /// <summary>
    /// Opens the per-item editor. The dialog returns the sessions to keep; when the
    /// user saved (result not null) they are persisted through the view model.
    /// </summary>
    private async void OpenEditor(EntryRow row)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new EditEntriesWindow(row);
        await dialog.ShowDialog(owner);

        if (dialog.Result is not { } sessions)
        {
            return;
        }

        _viewModel.ReplaceSessions(row.Task, sessions);
    }

    private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        var path = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // The view model owns the sort state; suppress the grid's own sorting so it
        // cannot fight the view-model order (which also spans all pages).
        e.Handled = true;
        _viewModel.ApplySort(path);
    }

    private async void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        // Edit mode now lives on F2 only; starting a task uses the play button.
        if (e.Key == Key.F2)
        {
            _grid.BeginEdit();
            e.Handled = true;
            return;
        }

        // Delete removes the selected entries (with confirmation).
        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await DeleteSelectedEntriesAsync();
        }
    }

    private async Task DeleteSelectedEntriesAsync()
    {
        var rows = _grid.SelectedItems?.OfType<EntryRow>().ToList() ?? [];
        if (rows.Count == 0)
        {
            return;
        }

        var summary = TrackerViewModel.BuildDeleteSummary(rows);
        var confirmed = await DeleteConfirmation.ConfirmAsync(this, summary);
        if (confirmed)
        {
            _viewModel.DeleteEntries(rows, _ => true);
        }
    }

    private void OnGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        var columnName = e.Column.SortMemberPath;
        if (columnName is not (nameof(EntryRow.Task) or nameof(EntryRow.BookingElement)))
        {
            return;
        }

        if (e.Row.DataContext is not EntryRow row)
        {
            return;
        }

        // Commit deferred, so a re-sort inside the view model cannot reenter the grid.
        Dispatcher.UIThread.Post(() => _viewModel.UpdateEntryText(row, row.Task, row.BookingElement));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrackerViewModel.SortColumn) or nameof(TrackerViewModel.SortAscending))
        {
            UpdateSortGlyphs();
        }
    }

    private void UpdateSortGlyphs()
    {
        foreach (var column in _grid.Columns)
        {
            if (string.IsNullOrEmpty(column.SortMemberPath))
            {
                continue;
            }
            if (!_columnBaseNames.TryGetValue(column.SortMemberPath, out var baseName))
            {
                continue;
            }

            if (column.SortMemberPath == _viewModel.SortColumn)
            {
                // Prominent direction icon right in the header text.
                column.Header = (_viewModel.SortAscending ? "▲ " : "▼ ") + baseName;
            }
            else
            {
                column.Header = baseName;
            }
        }
    }
}
