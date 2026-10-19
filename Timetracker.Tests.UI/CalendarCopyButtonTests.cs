using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.Plugins.Calendar.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// Every session block in the calendar has a copy button that copies the task
/// name, never its time or booking element.
/// </summary>
public sealed class CalendarCopyButtonTests
{
    private static readonly DateTimeOffset MondayNoon = new(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(2));

    [AvaloniaTest]
    public void CalendarWeekView_WhenRendered_ShouldShowOneCopyButtonPerBlock()
    {
        var view = Build(Session(9, "Report", "Project X"), Session(12, "Meeting", "Project Y"));
        Realize(view);

        CopyButtons(view).Should().HaveCount(2, "one per session block");
    }

    [AvaloniaTest]
    public async Task CalendarWeekView_WhenClickingABlockCopyButton_ShouldCopyOnlyTheTaskName()
    {
        var view = Build(Session(9, "Report", "Project X"), Session(14, "Review", "Project X"));

        var window = Realize(view);
        var copy = CopyButtons(view)[0];

        Click(copy);

        var text = await window.Clipboard!.TryGetTextAsync();
        text.Should().Be("Report", "the block's copy button copies the name without its time or element");
    }

    [AvaloniaTest]
    public async Task CalendarWeekView_WhenABlockHasNoBookingElement_ShouldStillCopyItsTaskName()
    {
        var view = Build(Session(9, "Adhoc"));

        var window = Realize(view);
        Click(CopyButtons(view)[0]);

        var text = await window.Clipboard!.TryGetTextAsync();
        text.Should().Be("Adhoc");
    }

    private static (DateTimeOffset Start, string Task, string Booking) Session(
        int hour, string task, string booking = "") =>
        (MondayNoon.Date.AddHours(hour), task, booking);

    private static CalendarTabView Build(
        params (DateTimeOffset Start, string Task, string Booking)[] sessions) =>
        CalendarTestSupport.Build(
            sessions.Select(s => CalendarTestSupport.Session(s.Start, s.Task, s.Booking)),
            now: () => MondayNoon).View;

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
        [.. view.GetVisualDescendants().OfType<Button>().Where(b => b.Content?.ToString() == "⧉")];
}
