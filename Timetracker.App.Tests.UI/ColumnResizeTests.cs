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
/// The tracker grid's data columns are user-resizable: dragging the separator
/// between two headers changes their widths. The two fixed-width action columns
/// stay put.
/// </summary>
public sealed class ColumnResizeTests
{
    [AvaloniaTest]
    public void TrackerTabView_WhenRendered_ShouldAllowResizingDataColumnsAndKeepActionColumnsFixed()
    {
        var (view, _) = Build();
        var window = new Window { Content = view, Width = 900, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var grid = Find<DataGrid>(view)!;
        grid.CanUserResizeColumns.Should().BeTrue();

        // Resizing is offered for the data columns but not the two action columns.
        grid.Columns.Should().HaveCount(7);
        grid.Columns[0].CanUserResize.Should().BeFalse("the edit action keeps a fixed width");
        grid.Columns[1].CanUserResize.Should().BeFalse("the play action keeps a fixed width");
        grid.Columns[2].CanUserResize.Should().BeTrue("Task is resizable");
        grid.Columns[3].CanUserResize.Should().BeTrue("Booking element is resizable");
        grid.Columns[4].CanUserResize.Should().BeTrue("Started is resizable");
        grid.Columns[5].CanUserResize.Should().BeTrue("Ended is resizable");
        grid.Columns[6].CanUserResize.Should().BeTrue("Duration is resizable");
    }

    [AvaloniaTest]
    public void TrackerTabView_WhenTaskHeaderSeparatorDragged_ShouldWidenTaskColumn()
    {
        var (view, _) = Build();
        var window = new Window { Content = view, Width = 900, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var grid = Find<DataGrid>(view)!;
        var taskHeader = grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => h.Content?.ToString() == "Task");

        var before = ColumnWidth(grid, "Task");
        var bookingBefore = ColumnWidth(grid, "Booking element");

        // Grab the separator at the right edge of the Task header and drag right,
        // in steps like a real pointer.
        var edge = taskHeader.TranslatePoint(
            new Point(taskHeader.Bounds.Width - 2, taskHeader.Bounds.Height / 2), window)!.Value;
        window.MouseMove(edge, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(edge, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        for (var dx = 10; dx <= 60; dx += 10)
        {
            window.MouseMove(edge + new Vector(dx, 0), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        window.MouseUp(edge + new Vector(60, 0), MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var after = ColumnWidth(grid, "Task");
        var bookingAfter = ColumnWidth(grid, "Booking element");
        TestContext.Out.WriteLine(
            $"Task {before:F1}->{after:F1}  Booking {bookingBefore:F1}->{bookingAfter:F1}");

        after.Should().BeGreaterThan(before, "dragging the separator widens Task");
        bookingAfter.Should().BeLessThan(bookingBefore, "the next column yields the space");

        // The fixed action columns are untouched.
        ColumnWidth(grid, "(action)").Should().Be(44);
    }

    private static string HeaderOf(DataGridColumn column) =>
        string.IsNullOrEmpty(column.Header?.ToString()) ? "(action)" : column.Header!.ToString()!;

    private static double ColumnWidth(DataGrid grid, string header) =>
        grid.Columns.First(c => HeaderOf(c) == header).ActualWidth;

    private static (TrackerTabView, TrackerViewModel) Build()
    {
        var repo = new FakeRepo([
            new TrackerEntry
            {
                Task = "Writing report", BookingElement = "Quarterly figures",
                Start = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2)),
                End = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(2)),
                Duration = "01:00:00", DurationSeconds = 3600,
            },
        ]);
        var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        var services = new ServiceCollection();
        services.AddSingleton<ITrackerRepository>(repo);
        services.AddSingleton<IUiTimer, FakeTimer>();
        services.AddSingleton<ITrackerUiHost, FakeHost>();
        return (new TrackerTabView(vm, services.BuildServiceProvider()), vm);
    }

    private static T? Find<T>(Control root) where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault();

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
