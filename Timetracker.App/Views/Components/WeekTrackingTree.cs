using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.ViewModels;

namespace Timetracker.Views.Components;

/// <summary>
/// The week's tracking tree: one collapsible day node per weekday (Monday first),
/// expanding to that day's booking element groups and, below them, the tasks
/// worked on that day. The gray per-day contributor line (e.g. PC activity) comes
/// from <see cref="WeekDayViewModel.ContributorText"/>. Each booking element and
/// task node carries a copy button that copies the node's task names. View-only;
/// the tree data lives in <see cref="WeekViewModel"/>.
/// </summary>
public sealed class WeekTrackingTree : UserControl
{
    /// <summary>Width of the +/- expander button.</summary>
    private const double ExpanderWidth = 18;

    /// <summary>Gap between an expander and the text that follows it.</summary>
    private const double RowSpacing = 4;

    /// <summary>One nesting step: how much deeper each level's text sits.</summary>
    private const double IndentSize = 18;

    /// <summary>
    /// Left edge of an element row: one step in, so its expander lines up under the
    /// day's text.
    /// </summary>
    private const double ElementRowIndent = IndentSize;

    /// <summary>
    /// Left edge of a task row. Task rows carry no expander, so the offset includes
    /// the expander column to place their text one indentation step past the element
    /// text rather than level with it.
    /// </summary>
    private const double EntryRowIndent =
        ElementRowIndent + ExpanderWidth + RowSpacing + IndentSize;

    private readonly WeekViewModel _week;

    private readonly StackPanel _tree = new();

    public WeekTrackingTree(WeekViewModel week)
    {
        _week = week;

        // The seven day view models are updated in place, so their property changes
        // must repaint the tree (ObservableCollection only reports add/remove).
        foreach (var day in _week.Days)
        {
            day.PropertyChanged += OnNodePropertyChanged;
        }
        _week.Days.CollectionChanged += OnDaysChanged;

        Content = new ScrollViewer { Content = _tree };
        UpdateTree();
    }

    private void OnDaysChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (WeekDayViewModel day in e.NewItems)
            {
                day.PropertyChanged += OnNodePropertyChanged;
            }
        }
        UpdateTree();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateTree();

    private void UpdateTree()
    {
        _tree.Children.Clear();

        foreach (var day in _week.Days)
        {
            _tree.Children.Add(BuildDayNode(day));

            if (!day.IsExpanded)
            {
                continue;
            }

            foreach (var group in day.Groups)
            {
                _tree.Children.Add(BuildElementNode(group));

                if (!group.IsExpanded)
                {
                    continue;
                }

                foreach (var entry in group.Entries)
                {
                    _tree.Children.Add(BuildEntryNode(entry));
                }
            }
        }
    }

    /// <summary>Day node: expander, caption, the day's sum and the PC activity line.</summary>
    private Control BuildDayNode(WeekDayViewModel day)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = RowSpacing,
            Margin = new Thickness(0, 2, 0, 2),
            Background = day.IsToday ? ViewBrushes.Today : ViewBrushes.Surface,
        };

        row.Children.Add(BuildExpander(day.IsExpanded, day.ToggleCommand));

        row.Children.Add(new TextBlock
        {
            Text = day.Header,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (day.TotalText.Length > 0)
        {
            row.Children.Add(new TextBlock
            {
                Text = day.TotalText,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var activity = day.ContributorText;
        if (activity.Length > 0)
        {
            row.Children.Add(new TextBlock
            {
                Text = activity,
                Foreground = ViewBrushes.Info,
                FontStyle = FontStyle.Italic,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return row;
    }

    /// <summary>
    /// Booking element node: expander, "Name (total)" and a copy button that copies
    /// the task names of the element's entries.
    /// </summary>
    private Control BuildElementNode(WeekElementGroupViewModel group)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = RowSpacing,
            Margin = new Thickness(ElementRowIndent, 1, 0, 1),
        };

        row.Children.Add(BuildExpander(group.IsExpanded, group.ToggleCommand));
        row.Children.Add(new TextBlock
        {
            Text = $"{group.Name} ({group.TotalText})",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(BuildCopyButton(group.CopyText, "Copy this element's tasks"));

        return row;
    }

    /// <summary>Task node: "Task (total)" and a copy button that copies the task name.</summary>
    private Control BuildEntryNode(WeekEntryViewModel entry)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = RowSpacing,
            Margin = new Thickness(EntryRowIndent, 1, 0, 1),
        };

        row.Children.Add(new TextBlock
        {
            Text = $"{entry.Task} ({entry.TotalText})",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(BuildCopyButton(entry.CopyText, "Copy this task"));

        return row;
    }

    private static Control BuildExpander(bool isExpanded, System.Windows.Input.ICommand command)
    {
        var button = new Button
        {
            Content = isExpanded ? "−" : "+",
            FontSize = 11,
            Width = 18,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Command = command;
        return button;
    }

    private Control BuildCopyButton(string copyText, string tooltip)
    {
        var button = new Button
        {
            Content = "⧉",
            FontSize = 10,
            Padding = new Thickness(3, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, tooltip);

        button.Click += async (_, _) => await CopyAsync(copyText);
        return button;
    }

    private async Task CopyAsync(string copyText)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            _week.ShowStatus("Could not access the clipboard.", WeekStatus.Error);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(copyText);
            _week.ShowStatus("Copied the tracked tasks.", WeekStatus.Success);
        }
        catch (Exception ex)
        {
            _week.ShowStatus("Could not copy: " + ex.Message, WeekStatus.Error);
        }
    }
}
