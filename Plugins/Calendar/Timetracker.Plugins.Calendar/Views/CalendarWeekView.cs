using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Timetracker.Plugins.Calendar.Localization;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Plugins.Calendar.Views;

/// <summary>
/// Outlook-style Workweek/Week grid: an hour axis on the left, one column per
/// visible day and time-proportional session blocks. Activity spans form a green
/// strip on each column's left edge, untracked gaps are warning bands with a Book
/// button and today's column carries a red now line. View-only; state lives in
/// <see cref="CalendarViewModel"/>.
/// </summary>
public sealed class CalendarWeekView : UserControl
{
    /// <summary>Width of the hour-label column shared by the header and the body.</summary>
    private const double AxisWidth = 56;

    /// <summary>Width of the green activity strip on a day column's left edge.</summary>
    private const double ActivityStripWidth = 3;

    /// <summary>Smallest height a short session block keeps so it stays clickable.</summary>
    private const double MinimumBlockHeight = 4;

    private readonly CalendarViewModel _calendar;
    private readonly Grid _headerGrid = new();
    private readonly Grid _bodyGrid = new();
    private readonly DispatcherTimer _timer;

    public CalendarWeekView(CalendarViewModel calendar)
    {
        _calendar = calendar;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => _calendar.RefreshNow();
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(_headerGrid);
        var scroll = new ScrollViewer
        {
            Content = _bodyGrid,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;

        _calendar.PropertyChanged += OnCalendarPropertyChanged;
        Rebuild();
    }

    private void OnCalendarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarViewModel.Mode) or nameof(CalendarViewModel.Days))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _headerGrid.Children.Clear();
        _headerGrid.ColumnDefinitions.Clear();
        _bodyGrid.Children.Clear();
        _bodyGrid.ColumnDefinitions.Clear();

        _headerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(AxisWidth)));
        _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(AxisWidth)));

        var visible = _calendar.VisibleDays;
        for (var i = 0; i < visible.Count; i++)
        {
            _headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var header = BuildDayHeader(visible[i]);
            Grid.SetColumn(header, i + 1);
            _headerGrid.Children.Add(header);
        }

        var axisCanvas = BuildAxis();
        Grid.SetColumn(axisCanvas, 0);
        _bodyGrid.Children.Add(axisCanvas);

        for (var i = 0; i < visible.Count; i++)
        {
            var body = BuildDayBody(visible[i]);
            Grid.SetColumn(body, i + 1);
            _bodyGrid.Children.Add(body);
        }

        _headerGrid.Height = double.NaN;
        _bodyGrid.Height = _calendar.Axis.Height;
    }

    private Control BuildAxis()
    {
        var axis = _calendar.Axis;
        var canvas = new Canvas { Width = AxisWidth, Height = axis.Height };

        for (var time = axis.Start; time <= axis.End; time += TimeSpan.FromHours(1))
        {
            var label = new TextBlock
            {
                Text = time.ToString(@"hh\:mm"),
                FontSize = 10,
                Foreground = ViewBrushes.Info,
                TextAlignment = TextAlignment.Right,
                Width = AxisWidth - 8,
            };
            Canvas.SetTop(label, Math.Max(0, CalendarLayout.Top(time, axis) - 7));
            canvas.Children.Add(label);
        }

        return canvas;
    }

    private Control BuildDayHeader(CalendarDayViewModel day)
    {
        var panel = new StackPanel { Spacing = 1 };

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        top.Children.Add(new TextBlock
        {
            Text = day.Header,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (day.TotalText.Length > 0)
        {
            top.Children.Add(new TextBlock
            {
                Text = day.TotalText,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        panel.Children.Add(top);

        if (day.ContributorText.Length > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = day.ContributorText,
                FontSize = 10,
                FontStyle = FontStyle.Italic,
                Foreground = ViewBrushes.Info,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (day.UntrackedText.Length > 0)
        {
            var untracked = new TextBlock
            {
                Text = day.UntrackedText,
                FontSize = 10,
                Foreground = day.IsUntrackedWarning ? ViewBrushes.Warning : ViewBrushes.Info,
                FontWeight = day.IsUntrackedWarning ? FontWeight.Bold : FontWeight.Normal,
                TextWrapping = TextWrapping.Wrap,
            };
            ToolTip.SetTip(untracked, Strings.Calendar_UntrackedToolTip);
            panel.Children.Add(untracked);
        }

        if (day.TotalText.Length > 0)
        {
            var distribute = new Button
            {
                Content = Strings.Calendar_Distribute,
                FontSize = 10,
                Padding = new Thickness(5, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                IsEnabled = day.CanDistribute,
            };
            ToolTip.SetTip(distribute, day.DistributionHint);
            ToolTip.SetShowOnDisabled(distribute, true);
            distribute.Click += async (_, _) => await DistributeAsync(day);
            panel.Children.Add(distribute);
        }

        return new Border
        {
            Background = day.IsToday ? ViewBrushes.Today : ViewBrushes.Surface,
            BorderBrush = ViewBrushes.CalendarGridLine,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(4, 2, 4, 3),
            Child = panel,
        };
    }

    private Control BuildDayBody(CalendarDayViewModel day)
    {
        var axis = _calendar.Axis;
        var canvas = new Canvas { Height = axis.Height, Background = Brushes.Transparent };
        var widthActions = new List<Action<double>>();

        for (var time = axis.Start; time <= axis.End; time += TimeSpan.FromHours(1))
        {
            var line = new Rectangle { Height = 1, Fill = ViewBrushes.CalendarGridLine };
            Canvas.SetTop(line, CalendarLayout.Top(time, axis));
            canvas.Children.Add(line);
            widthActions.Add(width => line.Width = width);
        }

        foreach (var span in day.ActiveSpans)
        {
            var strip = new Rectangle { Width = ActivityStripWidth, Fill = ViewBrushes.Success };
            var top = BlockTop(span.Start.TimeOfDay, axis);
            var height = BlockHeight(span.Start.TimeOfDay, span.Duration, axis);
            Canvas.SetTop(strip, top);
            Canvas.SetLeft(strip, 0);
            strip.Height = height;
            canvas.Children.Add(strip);
        }

        foreach (var gap in day.Gaps)
        {
            var band = BuildGapBand(gap);
            var top = BlockTop(gap.Range.Start.TimeOfDay, axis);
            var height = Math.Max(14, BlockHeight(gap.Range.Start.TimeOfDay, gap.Range.Duration, axis));
            Canvas.SetTop(band, top);
            band.Height = height;
            canvas.Children.Add(band);
            widthActions.Add(width =>
            {
                band.Width = width;
                Canvas.SetLeft(band, 0);
            });
        }

        var blocks = day.Sessions
            .Select(s => new TimeRange(s.Start, s.End))
            .ToList();
        var lanes = CalendarLayout.AssignLanes(blocks);
        for (var i = 0; i < day.Sessions.Count; i++)
        {
            var session = day.Sessions[i];
            var lane = lanes[i];
            var block = BuildBlock(session);
            var top = BlockTop(session.Start.TimeOfDay, axis);
            var height = Math.Max(MinimumBlockHeight, BlockHeight(session.Start.TimeOfDay, session.Duration, axis));
            Canvas.SetTop(block, top);
            block.Height = height;
            canvas.Children.Add(block);
            widthActions.Add(width =>
            {
                var laneWidth = Math.Max(18, (width - 4) / lane.LaneCount);
                block.Width = laneWidth;
                Canvas.SetLeft(block, 2 + (lane.Lane * laneWidth));
            });
        }

        if (day.IsToday && _calendar.NowLineMinutes is { } nowMinutes)
        {
            var nowLine = new Rectangle { Height = 2, Fill = ViewBrushes.Error };
            Canvas.SetTop(nowLine, nowMinutes / 60 * CalendarLayout.PixelsPerHour);
            canvas.Children.Add(nowLine);
            widthActions.Add(width => nowLine.Width = width);
        }

        canvas.SizeChanged += (_, e) =>
        {
            foreach (var action in widthActions)
            {
                action(e.NewSize.Width);
            }
        };

        return new Border
        {
            BorderBrush = ViewBrushes.CalendarGridLine,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = canvas,
        };
    }

    private Control BuildGapBand(CalendarGapViewModel gap)
    {
        var book = new Button
        {
            Content = Strings.Calendar_Book,
            FontSize = 10,
            Padding = new Thickness(5, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(book, gap.Text);
        book.Click += async (_, _) => await BookAsync(gap);

        var band = new Border
        {
            Background = ViewBrushes.GapBand,
            BorderBrush = ViewBrushes.Warning,
            BorderThickness = new Thickness(2, 0, 0, 0),
            Child = book,
        };
        ToolTip.SetTip(band, gap.Text);
        return band;
    }

    private Control BuildBlock(CalendarSessionViewModel session)
    {
        var caption = new TextBlock
        {
            Text = session.Task,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var copy = new Button
        {
            Content = "⧉",
            FontSize = 9,
            Padding = new Thickness(2, 0),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(copy, Strings.Calendar_CopyTask);
        copy.Click += async (_, _) => await CopyAsync(session.CopyText);

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(caption, 0);
        Grid.SetColumn(copy, 1);
        content.Children.Add(caption);
        content.Children.Add(copy);

        var block = new Border
        {
            Background = ViewBrushes.CalendarBlock,
            BorderBrush = session.IsRunning ? ViewBrushes.Success : ViewBrushes.CalendarBlockBorder,
            BorderThickness = new Thickness(session.IsRunning ? 2 : 1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1),
            Child = content,
        };
        ToolTip.SetTip(block, session.ToolTip);
        return block;
    }

    private static double BlockTop(TimeSpan start, CalendarAxis axis)
    {
        var clamped = start > axis.Start ? start : axis.Start;
        var result = CalendarLayout.Top(clamped, axis);
        return result;
    }

    private static double BlockHeight(TimeSpan start, TimeSpan duration, CalendarAxis axis)
    {
        var clampedStart = start > axis.Start ? start : axis.Start;
        var end = start + duration;
        var clampedEnd = end < axis.End ? end : axis.End;
        if (clampedEnd <= clampedStart)
        {
            return 0;
        }

        var result = (clampedEnd - clampedStart).TotalHours * CalendarLayout.PixelsPerHour;
        return result;
    }

    private async Task BookAsync(CalendarGapViewModel gap)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new BookGapWindow(_calendar.CreateBooking(gap));
        if (await dialog.ShowDialog<bool>(owner))
        {
            await _calendar.BookAsync(dialog.ViewModel);
        }
    }

    private async Task DistributeAsync(CalendarDayViewModel day)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new DistributeDayWindow(_calendar.CreateDistributeDay(day));
        if (await dialog.ShowDialog<bool>(owner))
        {
            await _calendar.ApplyDistributeAsync(day, dialog.ViewModel.Plan);
        }
    }

    private async Task CopyAsync(string copyText)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            _calendar.ShowStatus(Strings.Calendar_ClipboardError, CalendarStatusKind.Error);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(copyText);
            _calendar.ShowStatus(Strings.Calendar_Copied, CalendarStatusKind.Success);
        }
        catch (Exception ex)
        {
            _calendar.ShowStatus(string.Format(Strings.Calendar_CopyFailed, ex.Message), CalendarStatusKind.Error);
        }
    }
}
