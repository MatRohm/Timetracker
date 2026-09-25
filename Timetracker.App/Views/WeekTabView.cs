using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Timetracker.Plugins;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Week view tab: one column per weekday (Monday first) showing all booked times,
/// with navigation to previous/next/current week. View-only; state lives in
/// <see cref="WeekViewModel"/>. Additional per-day lines (e.g. PC activity) and
/// setup bands come from the registered <see cref="IWeekDayContributor"/> and
/// <see cref="IUiContributor"/> hooks.
/// </summary>
public sealed class WeekTabView : UserControl, IWeekStatusHost
{
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.FromRgb(0x69, 0x69, 0x69));
    private static readonly IBrush TodayBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE4, 0xB5));
    private static readonly IBrush SurfaceBrush = Brushes.Transparent;

    private readonly WeekViewModel _week;
    private readonly IServiceProvider _services;
    private readonly IReadOnlyList<IWeekDayContributor> _weekDayContributors;
    private readonly IReadOnlyList<IUiContributor> _uiContributors;

    private readonly TextBlock _titleLabel = new();
    private readonly TextBlock _totalLabel = new();
    private readonly Button _prevButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _currentButton = new();
    private readonly CheckBox _groupByBookingElementCheck = new();
    private readonly TextBlock _statusLabel = new();

    /// <summary>Grid holding the seven day columns and the contributor summary rows.</summary>
    private readonly Grid _daysGrid = new();

    public WeekTabView(WeekViewModel week, IServiceProvider services)
    {
        _week = week;
        _services = services;
        _weekDayContributors = services.GetServices<IWeekDayContributor>().ToArray();
        _uiContributors = services.GetServices<IUiContributor>()
            .Where(c => c.TargetTab == "Week view")
            .ToArray();

        // Add-in panels report results through the shared status line.
        UiHostAccessor.RegisterWeekStatusSink((message, kind) =>
            Dispatcher.UIThread.Post(() => ShowStatus(message, kind)));

        BuildUi();
        BindViewModel();
    }

    private void BuildUi()
    {
        var headerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _titleLabel.FontSize = 16;
        _titleLabel.FontWeight = FontWeight.Bold;
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.Margin = new Thickness(0, 0, 8, 6);
        Grid.SetColumn(_titleLabel, 0);
        headerRow.Children.Add(_titleLabel);

        _totalLabel.FontSize = 15;
        _totalLabel.FontWeight = FontWeight.Bold;
        _totalLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _totalLabel.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_totalLabel, 1);
        headerRow.Children.Add(_totalLabel);

        _prevButton.Content = "◀ Previous week";
        _nextButton.Content = "Next week ▶";
        _currentButton.Content = "● Current week";

        _groupByBookingElementCheck.Content = "Group by booking element";
        _groupByBookingElementCheck.IsChecked = true;
        _groupByBookingElementCheck.Margin = new Thickness(16, 0, 0, 0);
        _groupByBookingElementCheck.VerticalAlignment = VerticalAlignment.Center;

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 6),
        };
        buttonRow.Children.Add(_prevButton);
        buttonRow.Children.Add(_currentButton);
        buttonRow.Children.Add(_nextButton);
        buttonRow.Children.Add(_groupByBookingElementCheck);

        foreach (var contributor in _uiContributors)
        {
            buttonRow.Children.Add(contributor.CreateControl(_services));
        }

        // Seven equal day columns; rows are added dynamically (header, entries, summaries).
        for (var i = 0; i < 7; i++)
        {
            _daysGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        _statusLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _statusLabel.Foreground = InfoBrush;
        _statusLabel.Margin = new Thickness(0, 4, 0, 0);

        var root = new DockPanel { Margin = new Thickness(12, 10, 12, 10) };

        var topPanel = new StackPanel { Spacing = 4 };
        topPanel.Children.Add(headerRow);
        topPanel.Children.Add(buttonRow);
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        DockPanel.SetDock(_statusLabel, Dock.Bottom);
        root.Children.Add(_statusLabel);

        var scroll = new ScrollViewer { Content = _daysGrid };
        root.Children.Add(scroll);

        Content = root;
    }

    private void BindViewModel()
    {
        _prevButton.Command = _week.PreviousWeekCommand;
        _nextButton.Command = _week.NextWeekCommand;
        _currentButton.Command = _week.CurrentWeekCommand;

        _groupByBookingElementCheck.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding
        {
            Source = _week,
            Path = nameof(WeekViewModel.GroupByBookingElement),
            Mode = BindingMode.TwoWay,
        });

        _week.PropertyChanged += OnWeekPropertyChanged;
        // The seven day view models are updated in place, so their property changes
        // must repaint the columns (ObservableCollection only reports add/remove).
        foreach (var day in _week.Days)
        {
            day.PropertyChanged += OnDayPropertyChanged;
        }
        _week.Days.CollectionChanged += OnDaysChanged;
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
        if (e.PropertyName == nameof(WeekViewModel.WeekTitle))
        {
            _titleLabel.Text = _week.WeekTitle;
        }
        else if (e.PropertyName == nameof(WeekViewModel.WeekTotalText))
        {
            _totalLabel.Text = _week.WeekTotalText;
        }
        else if (e.PropertyName == nameof(WeekViewModel.GroupByBookingElement))
        {
            UpdateGrid();
        }
    }

    private void UpdateGrid()
    {
        _titleLabel.Text = _week.WeekTitle;
        _totalLabel.Text = _week.WeekTotalText;

        _daysGrid.Children.Clear();
        _daysGrid.RowDefinitions.Clear();

        // Row 0: weekday captions. Row 1: bookings per day.
        _daysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _daysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < _weekDayContributors.Count; c++)
        {
            _daysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var i = 0; i < 7; i++)
        {
            var day = _week.Days[i];

            var caption = new TextBlock
            {
                Text = day.Header,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(4, 0, 4, 4),
                Background = day.IsToday ? TodayBrush : SurfaceBrush,
            };
            header.Children.Add(caption);

            // Copying by booking element only makes sense while that grouping is on.
            if (_week.GroupByBookingElement)
            {
                header.Children.Add(BuildCopyDayButton(day));
            }

            Grid.SetColumn(header, i);
            Grid.SetRow(header, 0);
            _daysGrid.Children.Add(header);

            var entries = new TextBlock
            {
                Text = day.EntriesText,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 0, 4, 6),
                Background = day.IsToday ? TodayBrush : SurfaceBrush,
            };
            Grid.SetColumn(entries, i);
            Grid.SetRow(entries, 1);
            _daysGrid.Children.Add(entries);
        }

        // One gray summary row per contributor (e.g. PC activity).
        for (var c = 0; c < _weekDayContributors.Count; c++)
        {
            for (var i = 0; i < 7; i++)
            {
                var date = DateOnly.FromDateTime(_week.Days[i].Date.Date);
                var text = new TextBlock
                {
                    Text = _weekDayContributors[c].GetDayText(date),
                    Foreground = InfoBrush,
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
    /// A small copy button for one weekday column: copies that day's booking element
    /// names, one per line, to the clipboard and reports the result in the status line.
    /// </summary>
    private Control BuildCopyDayButton(WeekDayViewModel day)
    {
        var button = new Button
        {
            Content = "⧉",
            FontSize = 11,
            Padding = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, "Copy this day's booking elements");

        button.Click += async (_, _) => await CopyBookingElementsAsync(day);
        return button;
    }

    private async Task CopyBookingElementsAsync(WeekDayViewModel day)
    {
        var text = day.BookingElementNamesText;
        if (text.Length == 0)
        {
            ShowStatus("Nothing to copy for this day.", WeekStatusKind.Info);
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            ShowStatus("Could not access the clipboard.", WeekStatusKind.Error);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
            ShowStatus("Copied this day's booking elements.", WeekStatusKind.Success);
        }
        catch (Exception ex)
        {
            ShowStatus("Could not copy: " + ex.Message, WeekStatusKind.Error);
        }
    }

    /// <summary>One-line status at the bottom, styled like the tracker's status line.</summary>
    private void ShowStatus(string message, WeekStatusKind kind)
    {
        _statusLabel.Text = message;
        _statusLabel.Foreground = kind switch
        {
            WeekStatusKind.Success => SuccessBrush,
            WeekStatusKind.Error => ErrorBrush,
            _ => InfoBrush,
        };
    }

    void IWeekStatusHost.ShowStatus(string message, WeekStatusKind kind) => ShowStatus(message, kind);
}
