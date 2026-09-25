using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Models;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Dialog for one tracking item (a history row's task): lists every session with
/// editable start/end (the duration follows from them) and a per-session delete.
/// View-only; all rules live in <see cref="EditEntriesViewModel"/>.
///
/// On save the dialog exposes the item's remaining sessions through
/// <see cref="Result"/> (null when cancelled); the caller persists them.
/// </summary>
public sealed class EditEntriesWindow : Window
{
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22));

    private readonly EditEntriesViewModel _viewModel;

    /// <summary>Sessions to keep after an accepted edit; null when cancelled.</summary>
    public IReadOnlyList<TrackerEntry>? Result { get; private set; }

    public EditEntriesWindow(EntryRow item)
    {
        _viewModel = new EditEntriesViewModel(item);

        Title = "Edit entries – " + _viewModel.TaskName;
        Width = 880;
        Height = 460;
        MinWidth = 720;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildContent();
        KeyDown += OnKeyDown;
    }

    private Control BuildContent()
    {
        var header = new TextBlock
        {
            Text = _viewModel.TaskName,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var hint = new TextBlock
        {
            Text = "Adjust the start and end times; the duration is recalculated. "
                + "Use ✕ to delete a single session.",
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var footer = BuildFooter();
        var grid = BuildGrid();

        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(hint, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(hint);
        root.Children.Add(footer);
        root.Children.Add(grid);
        return root;
    }

    private Control BuildFooter()
    {
        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        summary.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(EditEntriesViewModel.SummaryText),
        });

        var error = new TextBlock
        {
            Foreground = ErrorBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "The end time must not be before the start time.",
        };
        error.Bind(TextBlock.IsVisibleProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(EditEntriesViewModel.HasErrors),
        });

        var saveButton = new Button { Content = "Save", IsDefault = true };
        saveButton.Bind(Button.IsEnabledProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(EditEntriesViewModel.CanSave),
        });
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Content = "Cancel", IsCancel = true };
        cancelButton.Click += (_, _) => Close();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        actions.Children.Add(cancelButton);
        actions.Children.Add(saveButton);

        var status = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        status.Children.Add(summary);
        status.Children.Add(error);

        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions);
        footer.Children.Add(status);
        return footer;
    }

    private DataGrid BuildGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            CanUserResizeColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            ItemsSource = _viewModel.Sessions,
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Task",
            Width = new DataGridLength(1.3, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(SessionEditRow.Task)),
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Start",
            Width = new DataGridLength(2.2, DataGridLengthUnitType.Star),
            CellTemplate = BuildStampTemplate(nameof(SessionEditRow.StartText)),
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "End",
            Width = new DataGridLength(2.2, DataGridLengthUnitType.Star),
            CellTemplate = BuildStampTemplate(nameof(SessionEditRow.EndText)),
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Duration",
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(SessionEditRow.DurationText)),
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            Width = new DataGridLength(48),
            CellTemplate = new FuncDataTemplate<SessionEditRow>((_, _) => BuildDeleteButton(), true),
        });
        return grid;
    }

    /// <summary>
    /// A text box editing one timestamp ("yyyy-MM-dd HH:mm"). The framework's
    /// DatePicker/TimePicker ignore Width and cannot be sized to fit a grid cell,
    /// so the same text format the history grid shows is edited directly.
    /// </summary>
    private static IDataTemplate BuildStampTemplate(string path) =>
        new FuncDataTemplate<SessionEditRow>((_, _) =>
        {
            var box = new TextBox
            {
                Watermark = "yyyy-MM-dd HH:mm",
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            box.Bind(TextBox.TextProperty, new Binding
            {
                Path = path,
                Mode = BindingMode.TwoWay,
            });
            return box;
        }, true);

    private Control BuildDeleteButton()
    {
        var button = new Button
        {
            Content = "✕",
            Padding = new Thickness(4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Delete this session");

        // The row is read from the button's DataContext at click time: the grid
        // recycles cells, so a captured row would go stale after a re-sort.
        button.Click += async (_, _) =>
        {
            if (button.DataContext is SessionEditRow row)
            {
                await DeleteAsync(row);
            }
        };
        return button;
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Save()
    {
        if (!_viewModel.CanSave)
        {
            return;
        }

        _viewModel.Save();
        Result = _viewModel.RemainingEntries();
        Close();
    }

    private async Task DeleteAsync(SessionEditRow row)
    {
        var confirmed = await ConfirmDeleteAsync(row);
        if (confirmed)
        {
            _viewModel.RemoveSession(row);
        }
    }

    private async Task<bool> ConfirmDeleteAsync(SessionEditRow row)
    {
        var dialog = new Window
        {
            Title = "Delete session",
            Width = 400,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var message = new TextBlock
        {
            Text = $"Delete the session {row.StartText} – {row.EndText} ({row.DurationText})?",
            TextWrapping = TextWrapping.Wrap,
        };

        var yes = new Button { Content = "Delete" };
        yes.Click += (_, _) => dialog.Close(true);
        var no = new Button { Content = "Cancel", IsCancel = true };
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

        return await dialog.ShowDialog<bool>(this);
    }
}
