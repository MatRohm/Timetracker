using Timetracker.Plugins.Interfaces;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Tracker tab: task input with suggestions, start/stop controls, status line and
/// the sortable, inline-editable history grid. View-only; logic lives in
/// <see cref="TrackerViewModel"/>. UI bands from add-ins (e.g. the Azure DevOps
/// import) come from the registered <see cref="IUiContributor"/> hooks.
/// </summary>
public sealed class TrackerTabView : UserControl, ITrackerUiHost
{
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.FromRgb(0x69, 0x69, 0x69));

    private readonly TrackerViewModel _vm;
    private readonly IServiceProvider _services;
    private readonly IReadOnlyList<IUiContributor> _uiContributors;

    private readonly TextBox _taskBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly TextBlock _elapsedLabel = new();
    private readonly TextBlock _statusLabel = new();
    private readonly DataGrid _grid = new();
    private readonly ListBox _suggestionList = new();
    private readonly Button _previousPageButton = new();
    private readonly TextBlock _pageLabel = new();
    private readonly Button _nextPageButton = new();
    private readonly StackPanel _pagerRow = new();

    /// <summary>Suppresses suggestion handling while a suggestion is being applied.</summary>
    private bool _pickingSuggestion;

    /// <summary>Header captions without sort indicator, keyed by column property name.</summary>
    private readonly Dictionary<string, string> _columnBaseNames = new();

    public Button StartButton => _startButton;

    public Button StopButton => _stopButton;

    public TrackerTabView(TrackerViewModel viewModel, IServiceProvider services)
    {
        _vm = viewModel;
        _services = services;
        _uiContributors = services.GetServices<IUiContributor>()
            .Where(c => c.TargetTab == "Tracker")
            .ToArray();

        // Add-in controls resolve the host interfaces through the registry.
        UiHostAccessor.RegisterTrackerHost(this);

        BuildUi();
        BindViewModel();
    }

    private void BuildUi()
    {
        var taskLabel = new TextBlock
        {
            Text = "Task name",
            Margin = new Thickness(0, 0, 0, 4),
        };

        _taskBox.Margin = new Thickness(0, 0, 0, 4);
        _taskBox.KeyDown += OnTaskBoxKeyDown;

        _suggestionList.IsVisible = false;
        _suggestionList.MaxHeight = 140;
        _suggestionList.ItemTemplate = new FuncDataTemplate<SuggestionItem>(
            (item, _) => new TextBlock { Text = item?.DisplayText ?? "" }, true);
        _suggestionList.DoubleTapped += OnSuggestionChosen;
        _suggestionList.KeyDown += OnSuggestionKeyDown;

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 4),
        };

        _startButton.Content = "▶ Start";
        _stopButton.Content = "■ Stop";

        buttonRow.Children.Add(_startButton);
        buttonRow.Children.Add(_stopButton);

        // Contributor controls go between Stop and the elapsed label.
        foreach (var contributor in _uiContributors)
        {
            buttonRow.Children.Add(contributor.CreateControl(_services));
        }

        _elapsedLabel.Text = "00:00:00";
        _elapsedLabel.FontFamily = new FontFamily("monospace");
        _elapsedLabel.FontSize = 20;
        _elapsedLabel.FontWeight = FontWeight.Bold;
        _elapsedLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _elapsedLabel.VerticalAlignment = VerticalAlignment.Center;

        var elapsedRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        elapsedRow.Children.Add(_elapsedLabel);
        buttonRow.Children.Add(elapsedRow);

        _statusLabel.TextWrapping = TextWrapping.NoWrap;
        _statusLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _statusLabel.Foreground = InfoBrush;
        _statusLabel.Margin = new Thickness(0, 2, 0, 4);

        _previousPageButton.Content = "◀ Previous";
        _nextPageButton.Content = "Next ▶";
        _pageLabel.VerticalAlignment = VerticalAlignment.Center;
        _pageLabel.Margin = new Thickness(8, 0, 8, 0);

        _pagerRow.Orientation = Orientation.Horizontal;
        _pagerRow.Spacing = 8;
        _pagerRow.Margin = new Thickness(0, 4, 0, 0);
        _pagerRow.Children.Add(_previousPageButton);
        _pagerRow.Children.Add(_pageLabel);
        _pagerRow.Children.Add(_nextPageButton);

        ConfigureGrid();

        var root = new DockPanel { Margin = new Thickness(12, 10, 12, 10) };

        var topPanel = new StackPanel { Spacing = 4 };
        topPanel.Children.Add(taskLabel);
        topPanel.Children.Add(_taskBox);
        topPanel.Children.Add(_suggestionList);
        topPanel.Children.Add(buttonRow);

        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        var bottomPanel = new StackPanel { Spacing = 2 };
        bottomPanel.Children.Add(_pagerRow);
        bottomPanel.Children.Add(_statusLabel);
        DockPanel.SetDock(bottomPanel, Dock.Bottom);
        root.Children.Add(bottomPanel);

        root.Children.Add(_grid);
        Content = root;
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

        _taskBox.Bind(TextBox.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.TaskName),
            Mode = BindingMode.TwoWay,
        });
        _taskBox.Bind(TextBox.IsReadOnlyProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.IsRunning),
        });
        _taskBox.TextChanged += OnTaskBoxTextChanged;

        _elapsedLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.ElapsedTimeText),
        });
        _statusLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.StatusText),
        });

        _grid.ItemsSource = _vm.Entries;
        _suggestionList.ItemsSource = _vm.Suggestions;

        _pageLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.PageText),
        });

        _grid.Sorting += OnGridSorting;
        _grid.KeyDown += OnGridKeyDown;
        _grid.CellEditEnded += OnGridCellEditEnded;

        BindCommand(_startButton, _vm.StartCommand);
        BindCommand(_stopButton, _vm.StopCommand);
        BindCommand(_previousPageButton, _vm.PreviousPageCommand);
        BindCommand(_nextPageButton, _vm.NextPageCommand);

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.Suggestions.CollectionChanged += OnSuggestionsChanged;
        _vm.InvalidTaskName += OnInvalidTaskName;

        UpdateSortGlyphs();
        UpdateSuggestions();
        UpdatePager();
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
        _grid.BorderThickness = new Thickness(0);
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

    private static void BindCommand(Button button, System.Windows.Input.ICommand command)
    {
        button.Command = command;
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
            Padding = new Thickness(4, 0),
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
            Padding = new Thickness(4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Start timing this task");

        button.Click += (_, _) =>
        {
            if (button.DataContext is EntryRow row)
            {
                _vm.StartFromRow(row);
            }
        };

        // A running session is never interrupted, so the action is unavailable then.
        button.Bind(Button.IsEnabledProperty, new Binding
        {
            Source = _vm,
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

        _vm.ReplaceSessions(row.Task, sessions);
    }

    private void OnTaskBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_pickingSuggestion)
            return;

        // The binding pushes the text into the view model; suggestions update with it.
        UpdateSuggestions();
    }

    private void OnTaskBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_suggestionList.IsVisible && _vm.Suggestions.Count > 0)
        {
            if (e.Key == Key.Down)
            {
                // Hand focus to the list so Arrow/Enter can pick a suggestion.
                _suggestionList.SelectedIndex = 0;
                _suggestionList.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideSuggestions();
                e.Handled = true;
            }
        }
    }

    private void OnSuggestionChosen(object? sender, TappedEventArgs e) => ApplySelectedSuggestion();

    private void OnSuggestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplySelectedSuggestion();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideSuggestions();
            _taskBox.Focus();
            e.Handled = true;
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
        var show = !_pickingSuggestion && _vm.ShowSuggestions;
        _suggestionList.IsVisible = show;
    }

    private void UpdatePager() => _pagerRow.IsVisible = _vm.HasMultiplePages;

    private void HideSuggestions() => _suggestionList.IsVisible = false;

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
        _vm.ApplySort(path);
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
        var confirmed = await ConfirmDeleteAsync(summary);
        if (confirmed)
        {
            _vm.DeleteEntries(rows, _ => true);
        }
    }

    private async Task<bool> ConfirmDeleteAsync(string summary)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = "Timetracker",
            Width = 380,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var message = new TextBlock
        {
            Text = $"Delete {summary}?\n\nThis removes the sessions from the JSON file and cannot be undone.",
            TextWrapping = TextWrapping.Wrap,
        };

        var yes = new Button { Content = "Yes" };
        yes.Click += (_, _) => dialog.Close(true);
        var no = new Button { Content = "No", IsCancel = true };
        no.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                message,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackerViewModel.Suggestions))
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
            _statusLabel.Foreground = _vm.Status switch
            {
                TrackerStatus.Success => SuccessBrush,
                TrackerStatus.Error => ErrorBrush,
                _ => InfoBrush,
            };
        }
    }

    private void OnSuggestionsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateSuggestions();

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

            if (column.SortMemberPath == _vm.SortColumn)
            {
                // Prominent direction icon right in the header text.
                column.Header = (_vm.SortAscending ? "▲ " : "▼ ") + baseName;
            }
            else
            {
                column.Header = baseName;
            }
        }
    }

    private void OnGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        var columnName = e.Column.SortMemberPath;
        if (columnName is not (nameof(EntryRow.Task) or nameof(EntryRow.BookingElement)))
            return;

        if (e.Row.DataContext is not EntryRow row)
            return;

        // Commit deferred, so a re-sort inside the view model cannot reenter the grid.
        Dispatcher.UIThread.Post(() => _vm.UpdateEntryText(row, row.Task, row.BookingElement));
    }

    private void OnInvalidTaskName()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var dialog = new Window
        {
            Title = "Timetracker",
            Width = 300,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var ok = new Button { Content = "OK", IsDefault = true };
        ok.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Please enter a task name first." },
                ok,
            },
        };
        _ = dialog.ShowDialog(owner);
        _taskBox.Focus();
    }

    void ITrackerUiHost.SetTaskName(string taskName) => _taskBox.Text = taskName;

    void ITrackerUiHost.SetBookingElement(string bookingElement) =>
        _vm.PreviewBookingElement = bookingElement;

    void ITrackerUiHost.ShowStatus(string message, TrackerStatusKind kind)
    {
        _statusLabel.Text = message;
        _statusLabel.Foreground = kind switch
        {
            TrackerStatusKind.Success => SuccessBrush,
            TrackerStatusKind.Error => ErrorBrush,
            _ => InfoBrush,
        };
    }
}
