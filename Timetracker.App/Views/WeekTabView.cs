using Timetracker.Plugins.Interfaces;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Threading;
using Timetracker.Plugins;
using Timetracker.ViewModels;
using Timetracker.Views.Components;

namespace Timetracker.Views;

/// <summary>
/// Week view tab: composes the week header/toolbar, the seven day columns and the
/// status line. View-only; state lives in <see cref="WeekViewModel"/>. Additional
/// per-day lines (e.g. PC activity) and setup bands come from the registered
/// <see cref="IWeekDayContributor"/> and <see cref="IUiContributor"/> hooks.
/// </summary>
public sealed class WeekTabView : UserControl, IWeekStatusHost
{
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

    private readonly WeekDaysGrid _daysGrid;

    public WeekTabView(WeekViewModel week, IServiceProvider services)
    {
        _week = week;
        _services = services;
        _weekDayContributors = services.GetServices<IWeekDayContributor>().ToArray();
        _uiContributors = services.GetServices<IUiContributor>()
            .Where(c => c.TargetTab == "Week view")
            .ToArray();

        _daysGrid = new WeekDaysGrid(_week, _weekDayContributors, this);

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

        // Add-in controls sit at the bottom of the view, directly above the status
        // line, so they do not mix with the week navigation toolbar.
        var contributorRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0),
            IsVisible = _uiContributors.Count > 0,
        };
        foreach (var contributor in _uiContributors)
        {
            contributorRow.Children.Add(contributor.CreateControl(_services));
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

        root.Children.Add(_daysGrid);
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
        UpdateHeader();
    }

    private void OnWeekPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeekViewModel.WeekTitle) or nameof(WeekViewModel.WeekTotalText))
        {
            UpdateHeader();
        }
    }

    private void UpdateHeader()
    {
        _titleLabel.Text = _week.WeekTitle;
        _totalLabel.Text = _week.WeekTotalText;
    }

    /// <summary>One-line status at the bottom, styled like the tracker's status line.</summary>
    private void ShowStatus(string message, WeekStatusKind kind)
    {
        _statusLabel.Text = message;
        _statusLabel.Foreground = kind switch
        {
            WeekStatusKind.Success => ViewBrushes.Success,
            WeekStatusKind.Error => ViewBrushes.Error,
            _ => ViewBrushes.Info,
        };
    }

    void IWeekStatusHost.ShowStatus(string message, WeekStatusKind kind) => ShowStatus(message, kind);
}
