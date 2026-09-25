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
using Timetracker.Services;
using Timetracker.Tests.Unit;
using Timetracker.ViewModels;
using Timetracker.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// While grouping by booking element, each booking element line in the week view
/// has a copy button that copies that element's name to the clipboard. Switching to
/// task grouping removes the buttons.
/// </summary>
public sealed class WeekCopyButtonTests
{
    [AvaloniaTest]
    public void Each_booking_element_line_has_a_copy_button()
    {
        var (view, _) = Build(Session(9, "Report", "Project X"), Session(12, "Meeting", "Project Y"));

        Realize(view);

        CopyButtonsForToday(view).Should().HaveCount(2, "one per booking element line");
    }

    [AvaloniaTest]
    public void Copy_buttons_disappear_in_task_grouping_mode()
    {
        var (view, week) = Build(Session(9, "Report", "Project X"));

        week.GroupByBookingElement = false;
        var window = Realize(view);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        CopyButtonsForToday(view).Should().BeEmpty("the copy button is only offered while grouping by element");
    }

    [AvaloniaTest]
    public void Clicking_a_line_copy_button_copies_that_booking_elements_name()
    {
        var (view, _) = Build(
            Session(9, "Report", "Project X"),
            Session(14, "Review", "Project X"),
            Session(12, "Meeting", "Project Y"));

        var window = Realize(view);
        // The first line in today's column is the earliest-started element (Project X).
        var button = CopyButtonsForToday(view)[0];

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        window.Clipboard!.GetTextAsync().GetAwaiter().GetResult()
            .Should().Be("Project X", "the button copies that booking element's name");
    }

    private static (DateTimeOffset Start, string Task, string Booking) Session(
        int hour, string task, string booking) =>
        (DateTimeOffset.Now.Date.AddHours(hour), task, booking);

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

    /// <summary>Copy buttons in today's column, so the tests are weekday-independent.</summary>
    private static List<Button> CopyButtonsForToday(Control view)
    {
        var todayColumn = ((int)DateTimeOffset.Now.DayOfWeek + 6) % 7; // Monday first
        var daysGrid = view.GetVisualDescendants().OfType<Grid>()
            .Single(g => g.ColumnDefinitions.Count == 7);
        var entriesCell = daysGrid.Children
            .OfType<Control>()
            .Single(c => Grid.GetRow(c) == 1 && Grid.GetColumn(c) == todayColumn);
        return [.. entriesCell.GetVisualDescendants().OfType<Button>()];
    }

    private sealed class FakeRepo : ITrackerRepository
    {
        public string FilePath => "memory.json";
        public IReadOnlyList<TrackerEntry> GetAll() => [];
        public void Add(TrackerEntry entry) { }
        public void Save(IReadOnlyList<TrackerEntry> e) { }
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
