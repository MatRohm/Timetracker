using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Calendar.Localization;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Plugins.Calendar.Views;

/// <summary>
/// Calendar tab: composes the header, the mode selector, the week or month view
/// and the status line. View-only; state lives in <see cref="CalendarViewModel"/>.
/// Add-in UI bands are passed in pre-built by the shell and placed above the
/// status line.
/// </summary>
public sealed class CalendarTabView : UserControl
{
    private readonly CalendarViewModel _calendar;
    private readonly IReadOnlyList<Control> _contributorControls;

    private readonly TextBlock _titleLabel = new();
    private readonly TextBlock _totalLabel = new();
    private readonly Button _prevButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _currentButton = new();
    private readonly Button _workweekButton = new();
    private readonly Button _weekButton = new();
    private readonly Button _monthButton = new();
    private readonly TextBlock _statusLabel = new();

    private readonly CalendarWeekView _weekView;
    private readonly CalendarMonthView _monthView;

    public CalendarTabView(CalendarViewModel calendar, IReadOnlyList<Control> contributorControls)
    {
        _calendar = calendar;
        _contributorControls = contributorControls;

        _weekView = new CalendarWeekView(_calendar);
        _monthView = new CalendarMonthView(_calendar);

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

        _prevButton.Content = Strings.Calendar_PreviousWeek;
        _nextButton.Content = Strings.Calendar_NextWeek;
        _currentButton.Content = Strings.Calendar_CurrentWeek;

        _workweekButton.Content = Strings.Calendar_ModeWorkweek;
        _weekButton.Content = Strings.Calendar_ModeWeek;
        _monthButton.Content = Strings.Calendar_ModeMonth;

        var modeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
        };
        modeRow.Children.Add(_workweekButton);
        modeRow.Children.Add(_weekButton);
        modeRow.Children.Add(_monthButton);

        var buttonRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var navigationRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        navigationRow.Children.Add(_prevButton);
        navigationRow.Children.Add(_currentButton);
        navigationRow.Children.Add(_nextButton);
        DockPanel.SetDock(modeRow, Dock.Right);
        buttonRow.Children.Add(modeRow);
        buttonRow.Children.Add(navigationRow);

        // Add-in controls sit at the bottom of the view, directly above the status
        // line, so they do not mix with the calendar toolbar.
        var contributorRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0),
            IsVisible = _contributorControls.Count > 0,
        };
        foreach (var control in _contributorControls)
        {
            contributorRow.Children.Add(control);
        }

        _statusLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _statusLabel.Foreground = ViewBrushes.Info;
        _statusLabel.Margin = new Thickness(0, 4, 0, 0);

        var content = new Grid();
        content.Children.Add(_weekView);
        content.Children.Add(_monthView);

        var root = new DockPanel { Margin = new Thickness(12, 10, 12, 10) };

        var topPanel = new StackPanel { Spacing = 4 };
        topPanel.Children.Add(headerRow);
        topPanel.Children.Add(buttonRow);
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        var bottomPanel = new StackPanel { Spacing = 2 };
        bottomPanel.Children.Add(contributorRow);
        bottomPanel.Children.Add(_statusLabel);
        DockPanel.SetDock(bottomPanel, Dock.Bottom);
        root.Children.Add(bottomPanel);

        root.Children.Add(content);
        Content = root;
    }

    private void BindViewModel()
    {
        _prevButton.Command = _calendar.PreviousCommand;
        _nextButton.Command = _calendar.NextCommand;
        _currentButton.Command = _calendar.CurrentCommand;
        _workweekButton.Command = _calendar.WorkweekCommand;
        _weekButton.Command = _calendar.WeekCommand;
        _monthButton.Command = _calendar.MonthCommand;

        _calendar.PropertyChanged += OnCalendarPropertyChanged;
        UpdateHeader();
        UpdateStatus();
        UpdateMode();
    }

    private void OnCalendarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarViewModel.CalendarTitle) or nameof(CalendarViewModel.CalendarTotalText))
        {
            UpdateHeader();
        }
        else if (e.PropertyName is nameof(CalendarViewModel.StatusText) or nameof(CalendarViewModel.Status))
        {
            UpdateStatus();
        }
        else if (e.PropertyName is nameof(CalendarViewModel.Mode))
        {
            UpdateMode();
        }
    }

    private void UpdateHeader()
    {
        _titleLabel.Text = _calendar.CalendarTitle;
        _totalLabel.Text = _calendar.CalendarTotalText;
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = _calendar.StatusText;
        _statusLabel.Foreground = _calendar.Status switch
        {
            CalendarStatusKind.Success => ViewBrushes.Success,
            CalendarStatusKind.Error => ViewBrushes.Error,
            _ => ViewBrushes.Info,
        };
    }

    private void UpdateMode()
    {
        var month = _calendar.Mode == CalendarViewMode.Month;
        _weekView.IsVisible = !month;
        _monthView.IsVisible = month;

        _prevButton.Content = month ? Strings.Calendar_PreviousMonth : Strings.Calendar_PreviousWeek;
        _nextButton.Content = month ? Strings.Calendar_NextMonth : Strings.Calendar_NextWeek;
        _currentButton.Content = month ? Strings.Calendar_CurrentMonth : Strings.Calendar_CurrentWeek;

        Highlight(_workweekButton, _calendar.Mode == CalendarViewMode.Workweek);
        Highlight(_weekButton, _calendar.Mode == CalendarViewMode.Week);
        Highlight(_monthButton, month);
    }

    private static void Highlight(Button button, bool active)
    {
        button.FontWeight = active ? FontWeight.Bold : FontWeight.Normal;
        button.Background = active ? ViewBrushes.Today : ViewBrushes.Surface;
    }
}
