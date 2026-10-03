using Avalonia.Controls;
using Avalonia.Platform;
using Timetracker.App.Localization;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Views;

/// <summary>
/// Application shell: hosts the tracker, week and options tabs, binds the window title and
/// app-level events. All tab content lives in the dedicated tab views; add-in UI
/// controls are resolved by the composition root and passed in pre-built.
/// </summary>
public sealed class TrackerWindow : Window
{
    private readonly TrackerViewModel _vm;
    private readonly TrackerTabView _trackerView;

    public TrackerWindow(
        TrackerViewModel viewModel,
        IReadOnlyList<Control> trackerContributors,
        IReadOnlyList<TabItem> pluginTabs,
        OptionsViewModel options)
    {
        _vm = viewModel;

        Title = Strings.App_Title;
        Width = 900;
        Height = 620;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = LoadAppIcon();

        _trackerView = new TrackerTabView(_vm, trackerContributors);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = Strings.App_TabTracker, Content = _trackerView });
        foreach (var tab in pluginTabs)
        {
            tabs.Items.Add(tab);
        }
        tabs.Items.Add(new TabItem { Header = Strings.App_TabOptions, Content = new OptionsTabView(options) });

        Content = tabs;

        // Bind the window title to the view model (shows the tracked task).
        this.Bind(TitleProperty, new Avalonia.Data.Binding
        {
            Source = _vm,
            Path = nameof(TrackerViewModel.Title),
        });

        Closing += OnClosing;
    }

    private static WindowIcon? LoadAppIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Timetracker/Timetracker.png"));
            return new WindowIcon(stream);
        }
        catch (Exception)
        {
            // A missing icon must never prevent the app from starting.
            return null;
        }
    }

    private bool _closing;

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Never lose a running entry: the view model saves it before the window closes.
        // Hold the window open while the write completes so the UI thread stays free;
        // Close() after the save re-raises Closing, so the guard stops the loop.
        if (_closing)
        {
            return;
        }

        e.Cancel = true;
        _closing = true;
        await _vm.SaveRunningEntryOnCloseAsync();
        Close();
    }
}
