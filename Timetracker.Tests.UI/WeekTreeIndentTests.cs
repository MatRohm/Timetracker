using Timetracker.Interfaces;
using Timetracker.Plugins.Interfaces;
using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
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
/// The week tree indents each level: the day's text sits left of its booking
/// element's, which sits left of its task's. The task row carries no expander, so
/// its indent has to account for the element's expander column.
/// </summary>
public sealed class WeekTreeIndentTests
{
    [AvaloniaTest]
    public void WeekTrackingTree_WhenExpanded_ShouldIndentEachLevelPastItsParent()
    {
        var (view, week) = Build();
        ExpandToday(week);

        Realize(view);

        var dayX = TextX(view, "Mon");
        var elementX = TextX(view, "Project X (1:00)");
        var taskX = TextX(view, "Report (1:00)");

        elementX.Should().BeGreaterThan(dayX, "the booking element is indented past the day");
        taskX.Should().BeGreaterThan(elementX, "the task is indented past its booking element");
    }

    private static double TextX(Control view, string startsWith)
    {
        var label = view.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Text?.StartsWith(startsWith, StringComparison.Ordinal) == true);
        return label.TranslatePoint(default, view)!.Value.X;
    }

    private static void ExpandToday(WeekViewModel week)
    {
        var today = week.Days.Single(d => d.IsToday);
        today.IsExpanded = true;
        foreach (var group in today.Groups)
        {
            group.IsExpanded = true;
        }
    }

    private static (WeekTabView View, WeekViewModel Week) Build()
    {
        var repo = new FakeRepo();
        using var tracker = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        tracker.Week.UpdateSessions(
        [
            new TrackerEntry
            {
                Task = "Report",
                BookingElement = "Project X",
                Start = DateTimeOffset.Now.Date.AddHours(9),
                End = DateTimeOffset.Now.Date.AddHours(10),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            },
        ]);

        var services = new ServiceCollection();
        services.AddSingleton<ITrackerRepository>(repo);
        services.AddSingleton<IUiTimer, FakeTimer>();
        services.AddSingleton<ITrackerUiHost, FakeHost>();
        services.AddSingleton<IWeekStatusHost>(new FakeWeekStatusHost());
        return (new WeekTabView(tracker.Week, services.BuildServiceProvider()), tracker.Week);
    }

    private static void Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 500 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class FakeRepo : ITrackerRepository
    {
        public string FilePath => "memory.json";
        public Task<IReadOnlyList<TrackerEntry>> GetAllAsync() => Task.FromResult<IReadOnlyList<TrackerEntry>>([]);
        public Task AddAsync(TrackerEntry entry) => Task.CompletedTask;
        public Task SaveAsync(IReadOnlyList<TrackerEntry> e) => Task.CompletedTask;
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

    private sealed class FakeWeekStatusHost : IWeekStatusHost
    {
        public void ShowStatus(string message, WeekStatusKind kind) { }
    }
}
