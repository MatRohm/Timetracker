using Timetracker.Interfaces;
using Timetracker.Plugins.Interfaces;
using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Timetracker.Models;
using Timetracker.Plugins;
using Timetracker.Tests.Unit;
using Timetracker.ViewModels;
using Timetracker.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// In the week tree, each booking element row and each task row has a copy button
/// that copies the task names of that node, never its time.
/// </summary>
public sealed class WeekCopyButtonTests
{
    [AvaloniaTest]
    public void WeekTrackingTree_WhenRendered_ShouldShowCopyButtonsOnElementAndTaskRows()
    {
        var (view, week) = Build(Session(9, "Report", "Project X"), Session(12, "Meeting", "Project Y"));
        ExpandToday(week);
        Realize(view);

        // One task row per entry plus one element row per element.
        CopyButtons(view).Should().HaveCount(4, "two elements and their two tasks");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenClickingElementCopyButton_ShouldCopyItsTaskNames()
    {
        var (view, week) = Build(
            Session(9, "Report", "Project X"),
            Session(14, "Review", "Project X"));
        ExpandToday(week);

        var window = Realize(view);
        // Rows are ordered oldest first: element "Project X", then its tasks.
        var elementCopy = CopyButtons(view)[0];

        Click(elementCopy);

        window.Clipboard!.GetTextAsync().GetAwaiter().GetResult()
            .Should().Be("Report" + Environment.NewLine + "Review",
                "the element's copy button copies the task names inside it, not the element name");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenClickingTaskCopyButton_ShouldCopyOnlyTheTaskName()
    {
        var (view, week) = Build(Session(9, "Report", "Project X"), Session(14, "Review", "Project X"));
        ExpandToday(week);

        var window = Realize(view);
        // Index 1 is the first task row under the element.
        var taskCopy = CopyButtons(view)[1];

        Click(taskCopy);

        window.Clipboard!.GetTextAsync().GetAwaiter().GetResult()
            .Should().Be("Report", "the task's copy button copies the name without its time");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenTheNoneGroupIsCopied_ShouldCopyItsTaskNames()
    {
        var (view, week) = Build(Session(9, "Adhoc"), Session(12, "Meeting", "Project Y"));
        ExpandToday(week);

        var window = Realize(view);
        // The row whose own child text starts with "<None>"; its copy button is the ⧉.
        var noneLabel = FindAll<TextBlock>(view)
            .First(t => t.Text?.StartsWith(WeekDayViewModel.NoBookingElementLabel) == true);
        var noneRow = noneLabel.Parent.Should().BeOfType<StackPanel>().Subject;
        var copy = noneRow.GetVisualDescendants().OfType<Button>()
            .First(b => b.Content?.ToString() == "⧉");

        Click(copy);

        window.Clipboard!.GetTextAsync().GetAwaiter().GetResult().Should().Be("Adhoc");
    }

    private static (DateTimeOffset Start, string Task, string Booking) Session(
        int hour, string task, string booking = "") =>
        (DateTimeOffset.Now.Date.AddHours(hour), task, booking);

    /// <summary>Expands today's day and its groups so the rows below become visible.</summary>
    private static void ExpandToday(WeekViewModel week)
    {
        var today = week.Days.Single(d => d.IsToday);
        today.IsExpanded = true;
        foreach (var group in today.Groups)
        {
            group.IsExpanded = true;
        }
    }

    private static (WeekTabView View, WeekViewModel Week) Build(
        params (DateTimeOffset Start, string Task, string Booking)[] sessions)
    {
        var repo = new FakeRepo();
        using var tracker = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        tracker.Week.UpdateSessions(
        [
            .. sessions.Select(s => new TrackerEntry
            {
                Task = s.Task,
                BookingElement = s.Booking,
                Start = s.Start,
                End = s.Start.AddHours(1),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            }),
        ]);

        var services = new ServiceCollection();
        services.AddSingleton<ITrackerRepository>(repo);
        services.AddSingleton<IUiTimer, FakeTimer>();
        services.AddSingleton<ITrackerUiHost, FakeHost>();
        services.AddSingleton<IWeekStatusHost>(new FakeWeekStatusHost());
        return (new WeekTabView(tracker.Week, services.BuildServiceProvider()), tracker.Week);
    }

    private static Window Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 500 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return host;
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<Button> CopyButtons(Control view) =>
        [.. FindAll<Button>(view).Where(b => b.Content?.ToString() == "⧉")];

    private static IEnumerable<T> FindAll<T>(Control root) where T : Control =>
        root.GetVisualDescendants().OfType<T>();

    private sealed class FakeRepo : ITrackerRepository
    {
        public string FilePath => "memory.json";
        public Task<IReadOnlyList<TrackerEntry>> GetAllAsync() => Task.FromResult<IReadOnlyList<TrackerEntry>>([]);
        public Task AddAsync(TrackerEntry entry) => Task.CompletedTask;
        public Task SaveAsync(IReadOnlyList<TrackerEntry> e) => Task.CompletedTask;
    }

    private sealed class FakeHost : ITrackerUiHost
    {
        public void SetTaskName(string taskName) { }
        public void SetBookingElement(string bookingElement) { }
        public void ShowStatus(string message, TrackerStatusKind kind) { }
    }

    private sealed class FakeWeekStatusHost : IWeekStatusHost
    {
        public void ShowStatus(string message, WeekStatusKind kind) { }
    }
}
