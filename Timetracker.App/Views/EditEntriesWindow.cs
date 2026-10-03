using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Contracts.Ui;
using Timetracker.Plugins.Contracts.ViewModels;
using Timetracker.App.Localization;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Views;

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

    /// <summary>True once the user saved; false when the dialog was cancelled.</summary>
    public bool Saved { get; private set; }

    /// <summary>The editor whose remaining sessions the caller persists.</summary>
    public EditEntriesViewModel ViewModel => _viewModel;

    public EditEntriesWindow(EntryRow item)
    {
        _viewModel = new EditEntriesViewModel(item);

        Title = string.Format(Strings.EditEntries_Title, _viewModel.TaskName);
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
            Text = Strings.EditEntries_Hint,
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
            Text = Strings.EditEntries_EndBeforeStart,
        };
        error.Bind(TextBlock.IsVisibleProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(EditEntriesViewModel.HasErrors),
        });

        var saveButton = new Button { Content = Strings.EditEntries_Save, IsDefault = true };
        saveButton.Bind(Button.IsEnabledProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(EditEntriesViewModel.CanSave),
        });
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Content = Strings.EditEntries_Cancel, IsCancel = true };
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
            Header = Strings.EditEntries_TaskHeader,
            Width = new DataGridLength(1.3, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(SessionEditRow.Task)),
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = Strings.EditEntries_StartHeader,
            Width = new DataGridLength(2.2, DataGridLengthUnitType.Star),
            CellTemplate = BuildStampTemplate(nameof(SessionEditRow.StartText)),
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = Strings.EditEntries_EndHeader,
            Width = new DataGridLength(2.2, DataGridLengthUnitType.Star),
            CellTemplate = BuildStampTemplate(nameof(SessionEditRow.EndText)),
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = Strings.EditEntries_DurationHeader,
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
        ToolTip.SetTip(button, Strings.EditEntries_DeletionToolTip);

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
        Saved = true;
        Close();
    }

    private async Task DeleteAsync(SessionEditRow row)
    {
        var message = string.Format(Strings.EditEntries_DeleteConfirm, row.StartText, row.EndText, row.DurationText);
        var confirmed = await Confirmations.ConfirmAsync(this, message, acceptLabel: Strings.EditEntries_Delete);
        if (confirmed)
        {
            _viewModel.RemoveSession(row);
        }
    }
}
