using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Contracts.Ui;
using Timetracker.App.ViewModels;
using Timetracker.App.Views.Components;

namespace Timetracker.App.Views;

/// <summary>
/// Tracker tab: composes the task input (with autocomplete), the start/stop toolbar
/// with the elapsed clock, the history grid, the pager and the status line.
/// View-only; logic lives in <see cref="TrackerViewModel"/>. Add-in UI bands are
/// passed in pre-built by the composition root and placed in the toolbar.
/// </summary>
public sealed class TrackerTabView : UserControl
{
    private readonly TrackerViewModel _vm;
    private readonly IReadOnlyList<Control> _contributorControls;

    private readonly TaskInputField _taskInput;
    private readonly EntryHistoryGrid _historyGrid;
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly TextBlock _elapsedLabel = new();
    private readonly TextBlock _statusLabel = new();
    private readonly Button _previousPageButton = new();
    private readonly TextBlock _pageLabel = new();
    private readonly Button _nextPageButton = new();
    private readonly StackPanel _pagerRow = new();

    public Button StartButton => _startButton;

    public Button StopButton => _stopButton;

    public TrackerTabView(TrackerViewModel viewModel, IReadOnlyList<Control> contributorControls)
    {
        _vm = viewModel;
        _contributorControls = contributorControls;

        _taskInput = new TaskInputField(_vm);
        _historyGrid = new EntryHistoryGrid(_vm);

        BuildUi();
        BindToolbar();
    }

    private void BuildUi()
    {
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 4),
        };

        _startButton.Content = "▶ Start";
        _stopButton.Content = "■ Stop";
        buttonRow.Children.Add(_startButton);
        buttonRow.Children.Add(_stopButton);

        // Contributor controls go between Stop and the elapsed label.
        foreach (var control in _contributorControls)
        {
            buttonRow.Children.Add(control);
        }

        _elapsedLabel.Text = "00:00:00";
        _elapsedLabel.FontFamily = new FontFamily("monospace");
        _elapsedLabel.FontSize = 20;
        _elapsedLabel.FontWeight = FontWeight.Bold;
        _elapsedLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _elapsedLabel.VerticalAlignment = VerticalAlignment.Center;

        buttonRow.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _elapsedLabel },
        });

        _statusLabel.TextWrapping = TextWrapping.NoWrap;
        _statusLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _statusLabel.Foreground = ViewBrushes.Info;
        _statusLabel.Margin = new Thickness(0, 2, 0, 4);

        _previousPageButton.Content = "◀ Previous";
        _nextPageButton.Content = "Next ▶";
        _pageLabel.VerticalAlignment = VerticalAlignment.Center;
        _pageLabel.Margin = new Thickness(8, 0, 8, 0);

        _pagerRow.Orientation = Orientation.Horizontal;
        _pagerRow.Spacing = 8;
        _pagerRow.Margin = new Thickness(0, 4, 0, 0);
        _pagerRow.Children.Add(_previousPageButton);
        _pagerRow.Children.Add(_pageLabel);
        _pagerRow.Children.Add(_nextPageButton);

        var root = new DockPanel { Margin = new Thickness(12, 10, 12, 10) };

        var topPanel = new StackPanel { Spacing = 4 };
        topPanel.Children.Add(_taskInput);
        topPanel.Children.Add(buttonRow);
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        var bottomPanel = new StackPanel { Spacing = 2 };
        bottomPanel.Children.Add(_pagerRow);
        bottomPanel.Children.Add(_statusLabel);
        DockPanel.SetDock(bottomPanel, Dock.Bottom);
        root.Children.Add(bottomPanel);

        root.Children.Add(_historyGrid);
        Content = root;
    }

    private void BindToolbar()
    {
        _elapsedLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.ElapsedTimeText),
        });
        _statusLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.StatusText),
        });
        _pageLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.PageText),
        });

        _startButton.Command = _vm.StartCommand;
        _stopButton.Command = _vm.StopCommand;
        _previousPageButton.Command = _vm.PreviousPageCommand;
        _nextPageButton.Command = _vm.NextPageCommand;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.InvalidTaskName += OnInvalidTaskName;

        UpdatePager();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackerViewModel.TotalPages))
        {
            UpdatePager();
        }
        else if (e.PropertyName == nameof(TrackerViewModel.Status))
        {
            _statusLabel.Foreground = _vm.Status switch
            {
                TrackerStatus.Success => ViewBrushes.Success,
                TrackerStatus.Error => ViewBrushes.Error,
                _ => ViewBrushes.Info,
            };
        }
    }

    private void UpdatePager() => _pagerRow.IsVisible = _vm.HasMultiplePages;

    private void OnInvalidTaskName()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var dialog = new Window
        {
            Title = "Timetracker",
            Width = 300,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var ok = new Button { Content = "OK", IsDefault = true };
        ok.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Please enter a task name first." },
                ok,
            },
        };
        _ = dialog.ShowDialog(owner);
        _taskInput.TaskBox.Focus();
    }
}
