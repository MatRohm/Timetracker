using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Plugins.Calendar.Views;

/// <summary>
/// Month grid: six weeks of day cells, each showing the day total and up to three
/// task chips. Clicking a cell opens that week in Week mode. View-only; state
/// lives in <see cref="CalendarViewModel"/>.
/// </summary>
public sealed class CalendarMonthView : UserControl
{
    private static readonly IBrush CellBackground = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
    private static readonly IBrush GridLine = new SolidColorBrush(Color.FromArgb(0x30, 0x69, 0x69, 0x69));
    private static readonly IBrush ChipBackground = new SolidColorBrush(Color.FromRgb(0xD9, 0xE7, 0xF7));

    private readonly CalendarViewModel _calendar;
    private readonly Grid _grid = new();

    public CalendarMonthView(CalendarViewModel calendar)
    {
        _calendar = calendar;
        Content = _grid;
        _calendar.PropertyChanged += OnCalendarPropertyChanged;
        Rebuild();
    }

    private void OnCalendarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarViewModel.MonthDays) or nameof(CalendarViewModel.Mode))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();

        for (var column = 0; column < MonthGridBuilder.DaysPerWeek; column++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var row = 0; row <= MonthGridBuilder.Weeks; row++)
        {
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }

        for (var column = 0; column < MonthGridBuilder.DaysPerWeek; column++)
        {
            var label = new TextBlock
            {
                Text = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames[(column + 1) % 7],
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = ViewBrushes.Info,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2),
            };
            Grid.SetColumn(label, column);
            Grid.SetRow(label, 0);
            _grid.Children.Add(label);
        }

        for (var i = 0; i < _calendar.MonthDays.Count; i++)
        {
            var cell = BuildCell(_calendar.MonthDays[i]);
            Grid.SetColumn(cell, i % MonthGridBuilder.DaysPerWeek);
            Grid.SetRow(cell, 1 + (i / MonthGridBuilder.DaysPerWeek));
            _grid.Children.Add(cell);
        }
    }

    private Control BuildCell(MonthDayViewModel day)
    {
        var panel = new StackPanel { Spacing = 1 };

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        top.Children.Add(new TextBlock
        {
            Text = day.DayNumber,
            FontWeight = day.IsToday ? FontWeight.Bold : FontWeight.Normal,
        });
        if (day.TotalText.Length > 0)
        {
            top.Children.Add(new TextBlock
            {
                Text = day.TotalText,
                FontSize = 10,
                Foreground = ViewBrushes.Info,
            });
        }
        panel.Children.Add(top);

        foreach (var chip in day.Chips)
        {
            panel.Children.Add(new Border
            {
                Background = ChipBackground,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 0),
                Child = new TextBlock
                {
                    Text = chip,
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            });
        }

        if (day.HasMore)
        {
            panel.Children.Add(new TextBlock
            {
                Text = day.MoreText,
                FontSize = 9,
                Foreground = ViewBrushes.Info,
            });
        }

        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Opacity = day.IsInMonth ? 1 : 0.45,
            Content = panel,
        };
        button.Click += async (_, _) => await _calendar.ShowWeekAsync(day.Date);

        var cell = new Border
        {
            Background = day.IsToday ? ViewBrushes.Today : CellBackground,
            BorderBrush = GridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = button,
        };
        return cell;
    }
}
