using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Timetracker.Plugins;
using Timetracker.Plugins.Interfaces;
using Timetracker.ViewModels;

namespace Timetracker.Views.Components;

/// <summary>
/// The week's seven day columns (Monday first) with the per-day summary rows from
/// the registered <see cref="IWeekDayContributor"/> hooks (e.g. PC activity). Each
/// booking element line carries a copy button that copies that line's tracked task
/// names. View-only; the day data lives in <see cref="WeekViewModel"/>.
/// </summary>
public sealed class WeekDaysGrid : UserControl
{
    private const int DayColumns = 7;

    private readonly WeekViewModel _week;
    private readonly IReadOnlyList<IWeekDayContributor> _weekDayContributors;
    private readonly IWeekStatusHost _statusHost;

    /// <summary>Grid holding the seven day columns and the contributor summary rows.</summary>
    private readonly Grid _daysGrid = new();

    public WeekDaysGrid(WeekViewModel week, IReadOnlyList<IWeekDayContributor> weekDayContributors, IWeekStatusHost statusHost)
    {
        _week = week;
        _weekDayContributors = weekDayContributors;
        _statusHost = statusHost;

        foreach (var _ in Enumerable.Range(0, DayColumns))
        {
            _daysGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // The seven day view models are updated in place, so their property changes
        // must repaint the columns (ObservableCollection only reports add/remove).
        foreach (var day in _week.Days)
        {
            day.PropertyChanged += OnDayPropertyChanged;
        }
        _week.Days.CollectionChanged += OnDaysChanged;
        _week.PropertyChanged += OnWeekPropertyChanged;

        Content = new ScrollViewer { Content = _daysGrid };
        UpdateGrid();
    }

    private void OnDaysChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (WeekDayViewModel day in e.NewItems)
            {
                day.PropertyChanged += OnDayPropertyChanged;
            }
        }
        UpdateGrid();
    }

    private void OnDayPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateGrid();

    private void OnWeekPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeekViewModel.GroupByBookingElement))
        {
            UpdateGrid();
        }
    }

    private void UpdateGrid()
    {
        _daysGrid.Children.Clear();
        _daysGrid.RowDefinitions.Clear();

        // Row 0: weekday captions. Row 1: bookings per day.
        _daysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _daysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < _weekDayContributors.Count; c++)
        {
            _daysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var i = 0; i < DayColumns; i++)
        {
            var day = _week.Days[i];

            var header = new TextBlock
            {
                Text = day.Header,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(4, 0, 4, 4),
                Background = day.IsToday ? ViewBrushes.Today : ViewBrushes.Surface,
            };
            Grid.SetColumn(header, i);
            Grid.SetRow(header, 0);
            _daysGrid.Children.Add(header);

            var entries = new StackPanel
            {
                Margin = new Thickness(4, 0, 4, 6),
                Background = day.IsToday ? ViewBrushes.Today : ViewBrushes.Surface,
            };
            foreach (var group in day.Groups)
            {
                entries.Children.Add(BuildEntryLine(group));
            }
            Grid.SetColumn(entries, i);
            Grid.SetRow(entries, 1);
            _daysGrid.Children.Add(entries);
        }

        // One gray summary row per contributor (e.g. PC activity).
        for (var c = 0; c < _weekDayContributors.Count; c++)
        {
            for (var i = 0; i < DayColumns; i++)
            {
                var date = DateOnly.FromDateTime(_week.Days[i].Date.Date);
                var text = new TextBlock
                {
                    Text = _weekDayContributors[c].GetDayText(date),
                    Foreground = ViewBrushes.Info,
                    FontStyle = FontStyle.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 0, 4, 2),
                };
                Grid.SetColumn(text, i);
                Grid.SetRow(text, 2 + c);
                _daysGrid.Children.Add(text);
            }
        }
    }

    /// <summary>
    /// One booking element line (label plus a copy button) that copies the task
    /// names of that line's tracking entries. The button is only offered while
    /// grouping by booking element, because that is the grouping it refers to.
    /// </summary>
    private Control BuildEntryLine(WeekDayGroup group)
    {
        var label = new TextBlock
        {
            Text = group.Label,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var line = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
        };
        line.Children.Add(label);

        if (_week.GroupByBookingElement)
        {
            line.Children.Add(BuildCopyGroupButton(group));
        }

        return line;
    }

    private Control BuildCopyGroupButton(WeekDayGroup group)
    {
        var button = new Button
        {
            Content = "⧉",
            FontSize = 10,
            Padding = new Thickness(3, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Copy this booking element's tracked tasks");

        button.Click += async (_, _) => await CopyGroupAsync(group);
        return button;
    }

    private async Task CopyGroupAsync(WeekDayGroup group)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            _statusHost.ShowStatus("Could not access the clipboard.", WeekStatusKind.Error);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(group.CopyText);
            _statusHost.ShowStatus("Copied this booking element's tracked tasks.", WeekStatusKind.Success);
        }
        catch (Exception ex)
        {
            _statusHost.ShowStatus("Could not copy: " + ex.Message, WeekStatusKind.Error);
        }
    }
}
