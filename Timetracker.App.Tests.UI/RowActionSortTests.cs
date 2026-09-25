using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.Plugins;
using Timetracker.Services;
using Timetracker.Tests.Unit;
using Timetracker.ViewModels;
using Timetracker.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// After re-sorting the history grid, each row's edit and play buttons must still
/// act on the row they are drawn next to. The grid recycles its cells, so a button
/// that captured its row once would keep pointing at the row it was first built for.
/// </summary>
public sealed class RowActionSortTests
{
    [AvaloniaTest]
    public void Play_button_after_sorting_starts_the_row_it_is_drawn_next_to()
    {
        var (view, viewModel) = Build(
            Entry("Alpha", 9), Entry("Bravo", 12), Entry("Charlie", 15));

        var window = Realize(view);
        var grid = Find<DataGrid>(view)!;

        // Sort ascending by task; the default (newest-first) is alphabetical descending
        // here, so this is a full reversal of the visible order.
        viewModel.ApplySort(nameof(EntryRow.Task));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var expected = viewModel.Entries[0];
        var button = FindRowButton(grid, rowIndex: 0, "▶");

        Click(window, button);
        Dispatcher.UIThread.RunJobs();

        viewModel.TaskName.Should().Be(
            expected.Task, "the top row's play button must start the top row's task");
    }

    [AvaloniaTest]
    public void Edit_button_after_sorting_opens_the_row_it_is_drawn_next_to()
    {
        var (view, viewModel) = Build(
            Entry("Alpha", 9), Entry("Bravo", 12), Entry("Charlie", 15));

        var window = Realize(view);
        var grid = Find<DataGrid>(view)!;

        viewModel.ApplySort(nameof(EntryRow.Task));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var expected = viewModel.Entries[0];
        var button = FindRowButton(grid, rowIndex: 0, "✎");

        Click(window, button);
        Dispatcher.UIThread.RunJobs();

        var dialog = window.OwnedWindows.OfType<EditEntriesWindow>().LastOrDefault();
        dialog.Should().NotBeNull("clicking a row's edit icon opens its editor");
        dialog!.Title.Should().Contain(
            expected.Task, "the top row's edit icon must open the top row's editor");
    }

    private static TrackerEntry Entry(string task, int hour) => new()
    {
        Task = task,
        BookingElement = "Project",
        Start = new DateTimeOffset(2026, 9, 21, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 21, hour + 1, 0, 0, TimeSpan.FromHours(2)),
        Duration = "01:00:00",
        DurationSeconds = 3600,
    };

    private static Button FindRowButton(DataGrid grid, int rowIndex, string glyph)
    {
        var row = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .ElementAt(rowIndex);
        return row.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Content?.ToString() == glyph);
    }

    private static void Click(Window window, Button button)
    {
        var center = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
    }

    private static Window Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 400 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return host;
    }

    private static T? Find<T>(Control root) where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault();

    private static (TrackerTabView View, TrackerViewModel ViewModel) Build(params TrackerEntry[] entries)
    {
        var repo = new FakeRepo(entries);
        var viewModel = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var services = new ServiceCollection();
        services.AddSingleton<ITrackerRepository>(repo);
        services.AddSingleton<IUiTimer, FakeTimer>();
        services.AddSingleton<ITrackerUiHost, FakeHost>();
        return (new TrackerTabView(viewModel, services.BuildServiceProvider()), viewModel);
    }

    private sealed class FakeRepo(IReadOnlyList<TrackerEntry> entries) : ITrackerRepository
    {
        public string FilePath => "memory.json";
        public IReadOnlyList<TrackerEntry> GetAll() => entries;
        public void Add(TrackerEntry entry) { }
        public void Save(IReadOnlyList<TrackerEntry> e) { }
    }

    private sealed class FakeTimer : IUiTimer
    {
        public event Action? Tick { add { } remove { } }
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeHost : ITrackerUiHost
    {
        public void SetTaskName(string taskName) { }
        public void SetBookingElement(string bookingElement) { }
        public void ShowStatus(string message, TrackerStatusKind kind) { }
    }
}
