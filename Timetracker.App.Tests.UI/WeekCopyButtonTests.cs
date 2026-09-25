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
/// The week view offers a copy button per day that copies the day's booking
/// element names, one per line. It is only shown while grouping by booking element.
/// </summary>
public sealed class WeekCopyButtonTests
{
    [AvaloniaTest]
    public void Copy_buttons_are_shown_when_grouping_by_booking_element()
    {
        var (view, _) = Build(Session(9, "Report", "Project X"));

        Realize(view);

        CopyButtons(view).Should().HaveCount(7, "one copy button per weekday column");
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

        CopyButtons(view).Should().BeEmpty("copying by booking element is only offered while grouping by it");
    }

    [AvaloniaTest]
    public void Clicking_a_day_copy_button_copies_its_booking_element_names_one_per_line()
    {
        var (view, _) = Build(Session(9, "Report", "Project X"), Session(14, "Meeting", "Project Y"));

        var window = Realize(view);
        var button = CopyButtonForToday(view);

        Click(window, button);
        Dispatcher.UIThread.RunJobs();

        var copied = window.Clipboard!.GetTextAsync().GetAwaiter().GetResult();
        copied.Should().Be("Project X" + Environment.NewLine + "Project Y",
            "the day's booking elements, one per line in column order");
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

    private static void Click(Window window, Button button)
    {
        var center = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
    }

    /// <summary>The copy button in today's column, so the test is weekday-independent.</summary>
    private static Button CopyButtonForToday(Control view)
    {
        var todayColumn = ((int)DateTimeOffset.Now.DayOfWeek + 6) % 7; // Monday first
        return CopyButtonsInColumn(view, todayColumn).Single();
    }

    private static List<Button> CopyButtons(Control view) =>
        [.. Enumerable.Range(0, 7).SelectMany(column => CopyButtonsInColumn(view, column))];

    private static List<Button> CopyButtonsInColumn(Control view, int column)
    {
        var daysGrid = view.GetVisualDescendants().OfType<Grid>()
            .Single(g => g.ColumnDefinitions.Count == 7);
        return [.. daysGrid.Children
            .OfType<Control>()
            .Where(c => Grid.GetRow(c) == 0 && Grid.GetColumn(c) == column)
            .SelectMany(c => c.GetVisualDescendants().OfType<Button>())];
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
