using Avalonia.Controls;
using Avalonia.Platform;
using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Application shell: hosts the tracker and week tabs, binds the window title and
/// app-level events. All tab content lives in the dedicated tab views; add-in UI
/// comes from the registered hooks.
/// </summary>
public sealed class TrackerWindow : Window
{
    private readonly TrackerViewModel _vm;
    private readonly TrackerTabView _trackerView;

    public TrackerWindow(TrackerViewModel viewModel, IServiceProvider services)
    {
        _vm = viewModel;

        Title = "Timetracker";
        Width = 900;
        Height = 620;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = LoadAppIcon();

        _trackerView = new TrackerTabView(_vm, services);
        var weekView = new WeekTabView(_vm.Week, services);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Tracker", Content = _trackerView });
        tabs.Items.Add(new TabItem { Header = "Week view", Content = weekView });

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

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Never lose a running entry: the view model saves it before the window closes.
        _vm.SaveRunningEntryOnClose();
    }
}
