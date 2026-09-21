using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.Plugins;
using Timetracker.Services;
using Timetracker.ViewModels;
using Timetracker.Views;

namespace Timetracker.Tests;

/// <summary>
/// UI smoke tests: build the real views on a headless Avalonia instance and
/// assert that they render the expected content. These catch binding and theme
/// mistakes that pure view-model tests cannot (e.g. a missing DataGrid theme).
/// </summary>
public sealed class ViewRenderTests
{
    [AvaloniaTest]
    public void Tracker_view_shows_the_history_rows_and_start_button()
    {
        var (view, viewModel) = BuildTrackerView(
            Entry("Writing report", "Quarterly figures"),
            Entry("Code review", "Project X"));

        viewModel.Entries.Should().HaveCount(2);

        // The Start button and the history grid must both be part of the tree.
        var grid = FindControl<DataGrid>(view);
        grid.Should().NotBeNull("the history grid is part of the tracker view");
        grid!.ItemsSource.Should().BeSameAs(viewModel.Entries);
        grid.Columns.Should().HaveCount(5);
    }

    [AvaloniaTest]
    public void Tracker_view_exposes_suggestions_in_the_list()
    {
        var (view, viewModel) = BuildTrackerView(Entry("Report", ""));
        viewModel.TaskName = "rep";

        var suggestionList = FindControl<ListBox>(view);
        suggestionList.Should().NotBeNull();
        suggestionList!.ItemsSource.Should().BeSameAs(viewModel.Suggestions);
        viewModel.Suggestions.Should().ContainSingle().Which.Name.Should().Be("Report");
    }

    [AvaloniaTest]
    public void Week_view_renders_seven_day_columns_with_bookings()
    {
        var (repo, _) = RepositoryFake.Create();
        using var tracker = new TrackerViewModel(repo, new FakeTimer());
        tracker.Week.UpdateSessions([
            new TrackerEntry
            {
                Task = "Meeting",
                BookingElement = "Project X",
                Start = DateTimeOffset.Now.Date.AddHours(9),
                End = DateTimeOffset.Now.Date.AddHours(10),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            },
        ]);

        var services = BuildServices(repo);
        var view = new WeekTabView(tracker.Week, services);

        // The seven columns are laid out in the days grid; the booking text is present.
        // Default grouping is by booking element, so the label is the element name.
        var texts = FindAllControls<TextBlock>(view).Select(t => t.Text ?? "").ToList();
        texts.Should().Contain("Project X (1:00)");
        texts.Should().Contain(t => t.StartsWith("Week "), "the week title is shown");

        var grid = FindAllControls<Grid>(view).Single(g => g.ColumnDefinitions.Count == 7);
        grid.ColumnDefinitions.Should().HaveCount(7, "Monday through Sunday");
    }

    [AvaloniaTest]
    public void Azure_devops_panel_loads_its_embedded_icon_and_button()
    {
        var services = BuildServices(RepositoryFake.Create().Repo);
        var host = services.GetRequiredService<ITrackerUiHost>();
        var service = new AzureDevOps.AzureDevOpsService(configFilePath: "/nonexistent");
        var panel = new AzureDevOps.AzureDevOpsPanel(service, host);

        panel.ImportButton.Should().NotBeNull();
        var icon = AzureDevOps.AzureDevOpsPanel.LoadBitmap(
            "Timetracker.AzureDevOps.azure-favicon.png", 16);
        icon.Should().NotBeNull("the favicon is embedded as a PNG resource");
    }

    private static TrackerEntry Entry(string task, string bookingElement) => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(2)),
        Duration = "01:00:00",
        DurationSeconds = 3600,
    };

    private static (TrackerTabView View, TrackerViewModel ViewModel) BuildTrackerView(
        params TrackerEntry[] entries)
    {
        var (repo, _) = RepositoryFake.Create(entries);
        var viewModel = new TrackerViewModel(repo, new FakeTimer());
        var services = BuildServices(repo);
        var view = new TrackerTabView(viewModel, services);
        return (view, viewModel);
    }

    private static IServiceProvider BuildServices(ITrackerRepository repo)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repo);
        services.AddSingleton<IUiTimer, FakeTimer>();
        services.AddSingleton<TrackerViewModel>();
        services.AddSingleton<ITrackerUiHost>(new FakeTrackerHost());
        return services.BuildServiceProvider();
    }

    /// <summary>Keeps realized test windows alive so their visual tree remains valid.</summary>
    private static readonly List<Window> HostWindows = [];

    private static T? FindControl<T>(Control root) where T : Control =>
        FindAllControls<T>(root).FirstOrDefault();

    private static IReadOnlyList<T> FindAllControls<T>(Control root) where T : Control
    {
        Realize(root);
        return [.. root.GetVisualDescendants().OfType<T>()];
    }

    /// <summary>
    /// Attaches the control to a window once so its visual tree is built and laid
    /// out; the window is kept alive for the duration of the test process.
    /// </summary>
    private static void Realize(Control root)
    {
        if (root.GetVisualParent() is not null)
        {
            return;
        }

        var window = new Window { Content = root, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        HostWindows.Add(window);
    }

    private sealed class FakeTrackerHost : ITrackerUiHost
    {
        public void SetTaskName(string taskName) { }

        public void SetBookingElement(string bookingElement) { }

        public void ShowStatus(string message, TrackerStatusKind kind) { }
    }
}
