using System.ComponentModel;
using Timetracker.Plugins.Calendar.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Contracts.Ui;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Views;

/// <summary>
/// Calendar tab: composes the week header/toolbar, the seven day columns and the
/// status line. View-only; state lives in <see cref="CalendarViewModel"/>. Add-in UI
/// bands are passed in pre-built by the shell and placed above the status line.
/// </summary>
public sealed class CalendarTabView : UserControl
{
    private readonly CalendarViewModel _week;
    private readonly IReadOnlyList<Control> _contributorControls;

    private readonly TextBlock _titleLabel = new();
    private readonly TextBlock _totalLabel = new();
    private readonly Button _prevButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _currentButton = new();
    private readonly TextBlock _statusLabel = new();

    private readonly CalendarTrackingTree _tree;

    public CalendarTabView(CalendarViewModel week, IReadOnlyList<Control> contributorControls)
    {
        _week = week;
        _contributorControls = contributorControls;

        _tree = new CalendarTrackingTree(_week);

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

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 6),
        };
        buttonRow.Children.Add(_prevButton);
        buttonRow.Children.Add(_currentButton);
        buttonRow.Children.Add(_nextButton);

        // Add-in controls sit at the bottom of the view, directly above the status
        // line, so they do not mix with the week navigation toolbar.
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

        root.Children.Add(_tree);
        Content = root;
    }

    private void BindViewModel()
    {
        _prevButton.Command = _week.PreviousWeekCommand;
        _nextButton.Command = _week.NextWeekCommand;
        _currentButton.Command = _week.CurrentWeekCommand;

        _week.PropertyChanged += OnWeekPropertyChanged;
        UpdateHeader();
        UpdateStatus();
    }

    private void OnWeekPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarViewModel.CalendarTitle) or nameof(CalendarViewModel.CalendarTotalText))
        {
            UpdateHeader();
        }
        else if (e.PropertyName is nameof(CalendarViewModel.StatusText) or nameof(CalendarViewModel.Status))
        {
            UpdateStatus();
        }
    }

    private void UpdateHeader()
    {
        _titleLabel.Text = _week.CalendarTitle;
        _totalLabel.Text = _week.CalendarTotalText;
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = _week.StatusText;
        _statusLabel.Foreground = _week.Status switch
        {
            CalendarStatus.Success => ViewBrushes.Success,
            CalendarStatus.Error => ViewBrushes.Error,
            _ => ViewBrushes.Info,
        };
    }
}
