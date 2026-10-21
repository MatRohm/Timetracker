using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.Plugins.Calendar.Localization;
using Timetracker.Plugins.Calendar.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// The day header's copy button sits next to Distribute, opens a flyout with
/// one entry per booking element, and copies the chosen element's task names.
/// </summary>
public sealed class CalendarDayCopyButtonTests
{
    private static readonly DateTimeOffset MondayNoon = new(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(2));

    [AvaloniaTest]
    public void CalendarWeekView_WhenADayHasSessions_ShouldShowTheCopyButtonNextToDistribute()
    {
        var view = Build(Session(9, "Report", "Project X"));
        Realize(view);

        var copy = DayCopyButton(view);
        var actions = copy.GetVisualParent<StackPanel>();

        actions.Should().NotBeNull("the copy button sits in the day's action row");
        actions!.Children.OfType<Button>()
            .Select(button => button.Content?.ToString())
            .Should().Contain(Strings.Calendar_Distribute);
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenTheCopyButtonIsOpened_ShouldListEachElementWithItsTime()
    {
        var view = Build(Session(9, "Report", "Project X"), Session(12, "Meeting", "Project Y", 30));
        Realize(view);

        var copy = DayCopyButton(view);
        copy.Flyout!.ShowAt(copy);
        Dispatcher.UIThread.RunJobs();

        var items = TopLevel.GetTopLevel(view)!
            .GetVisualDescendants()
            .OfType<MenuItem>()
            .Where(item => item.Header is string)
            .ToList();

        items.Select(item => item.Header?.ToString())
            .Should().Equal("Project X (1:00)", "Project Y (0:30)");

        copy.Flyout.Hide();
    }

    [AvaloniaTest]
    public async Task CalendarWeekView_WhenAnElementIsChosen_ShouldCopyItsDistinctTaskNamesOnePerLine()
    {
        var view = Build(
            Session(9, "Report", "Project X"),
            Session(14, "Review", "Project X"),
            Session(16, "report", "Project X"));
        var window = Realize(view);
        var items = CopyItems(DayCopyButton(view));

        items[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var text = await window.Clipboard!.TryGetTextAsync();
        text.Should().Be("Report" + Environment.NewLine + "Review");
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenADayHasNoSessions_ShouldNotShowTheCopyButton()
    {
        var view = Build();
        Realize(view);

        view.GetVisualDescendants().OfType<Button>()
            .Should().NotContain(button => button.Flyout is MenuFlyout);
    }

    private static (DateTimeOffset Start, string Task, string Booking, int Minutes) Session(
        int hour, string task, string booking = "", int minutes = 60) =>
        (MondayNoon.Date.AddHours(hour), task, booking, minutes);

    private static CalendarTabView Build(
        params (DateTimeOffset Start, string Task, string Booking, int Minutes)[] sessions) =>
        CalendarTestSupport.Build(
            sessions.Select(session => CalendarTestSupport.Session(session.Start, session.Task, session.Booking, session.Minutes)),
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

    private static Button DayCopyButton(Control view) =>
        view.GetVisualDescendants().OfType<Button>().Single(button => button.Flyout is MenuFlyout);

    private static IReadOnlyList<MenuItem> CopyItems(Button copy)
    {
        var flyout = (MenuFlyout)copy.Flyout!;
        IReadOnlyList<MenuItem> result = [.. flyout.ItemsSource!.OfType<MenuItem>()];
        return result;
    }
}
