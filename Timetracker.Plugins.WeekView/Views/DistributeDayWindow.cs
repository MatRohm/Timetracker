using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Views;

/// <summary>
/// Dialog that distributes a day's untracked time over its tasks: one line per
/// task with −/+ steppers that move the task's share in 15-minute steps. The rows
/// start from a proportional prefill; the plan is shown live and applied by the
/// caller when the user distributes. View-only; the state lives in
/// <see cref="DistributeDayViewModel"/>. Closes with true when the user accepts,
/// false when cancelled.
/// </summary>
public sealed class DistributeDayWindow : Window
{
    public DistributeDayWindow(DistributeDayViewModel viewModel)
    {
        ViewModel = viewModel;

        Title = "Distribute untracked time";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildContent();
    }

    public DistributeDayViewModel ViewModel { get; }

    /// <summary>True when the user pressed Distribute (the caller applies the plan).</summary>
    public bool Saved { get; private set; }

    private Control BuildContent()
    {
        var summary = new TextBlock
        {
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
        };
        summary.Bind(TextBlock.TextProperty, new Binding
        {
            Source = ViewModel,
            Path = nameof(DistributeDayViewModel.MissingText),
        });
        var remaining = new TextBlock
        {
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        };
        remaining.Bind(TextBlock.TextProperty, new Binding
        {
            Source = ViewModel,
            Path = nameof(DistributeDayViewModel.RemainingText),
        });

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { summary, remaining },
        };
        Grid.SetColumn(remaining, 1);

        var rows = new ScrollViewer
        {
            MaxHeight = 320,
            Content = BuildRowGrid(),
        };

        var distribute = new Button { Content = "Distribute", IsDefault = true };
        distribute.Bind(IsEnabledProperty, new Binding
        {
            Source = ViewModel,
            Path = nameof(DistributeDayViewModel.CanAccept),
        });
        distribute.Click += (_, _) =>
        {
            Saved = true;
            Close(true);
        };

        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, distribute },
        };

        var result = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children = { header, rows, actions },
        };
        return result;
    }

    /// <summary>The header plus one line per task: name, current total, target and the steppers.</summary>
    private Grid BuildRowGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions(
                string.Join(",", ViewModel.Rows.Select(_ => "Auto").Prepend("Auto"))),
            ColumnSpacing = 8,
            RowSpacing = 6,
        };

        AddHeader(grid, 0, 0, "Task");
        AddHeader(grid, 0, 1, "Now");
        AddHeader(grid, 0, 2, "→");

        for (var i = 0; i < ViewModel.Rows.Count; i++)
        {
            AddRow(grid, i + 1, ViewModel.Rows[i]);
        }

        return grid;
    }

    private static void AddHeader(Grid grid, int row, int column, string caption)
    {
        var header = new TextBlock
        {
            Text = caption,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(header, row);
        Grid.SetColumn(header, column);
        grid.Children.Add(header);
    }

    private static void AddRow(Grid grid, int row, DistributeRowViewModel item)
    {
        var name = new TextBlock
        {
            Text = item.Task,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var now = new TextBlock
        {
            Text = item.CurrentText,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var target = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray,
        };
        target.Bind(TextBlock.TextProperty, new Binding
        {
            Source = item,
            Path = nameof(DistributeRowViewModel.TargetText),
        });
        ToolTip.SetTip(target, "The task's total once its assigned share is applied");

        var minus = new Button
        {
            Content = "−",
            FontSize = 11,
            Width = 18,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        minus.Command = item.DecreaseCommand;
        ToolTip.SetTip(minus, "Take 15 minutes back");

        var plus = new Button
        {
            Content = "+",
            FontSize = 11,
            Width = 18,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        plus.Command = item.IncreaseCommand;
        ToolTip.SetTip(plus, "Add 15 minutes to this task");

        var steppers = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { minus, plus },
        };

        Grid.SetRow(name, row);
        Grid.SetColumn(name, 0);
        Grid.SetRow(now, row);
        Grid.SetColumn(now, 1);
        Grid.SetRow(target, row);
        Grid.SetColumn(target, 2);
        Grid.SetRow(steppers, row);
        Grid.SetColumn(steppers, 3);
        grid.Children.Add(name);
        grid.Children.Add(now);
        grid.Children.Add(target);
        grid.Children.Add(steppers);
    }
}

