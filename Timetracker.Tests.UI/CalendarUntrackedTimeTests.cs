using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Tests.UI;

/// <summary>
/// The day header of the calendar shows the PC active time that no tracked
/// session covers, next to the day's booked total, and untracked gaps are warning
/// bands with a Book button.
/// </summary>
public sealed class CalendarUntrackedTimeTests
{
    private static readonly DateTimeOffset MondayNoon = new(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateOnly Monday = DateOnly.FromDateTime(MondayNoon.Date);

    [AvaloniaTest]
    public void CalendarWeekView_WhenActiveTimeExceedsTheBookedTime_ShouldShowTheUntrackedTimeInTheDayHeader()
    {
        var view = CalendarTestSupport.Build(
            [CalendarTestSupport.Session(MondayNoon.Date.AddHours(9), "Report")],
            activeTime: day => day == Monday ? TimeSpan.FromMinutes(150) : TimeSpan.Zero,
            now: () => MondayNoon).View;

        Realize(view);

        var labels = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        labels.Should().Contain("⚠ 1:30 untracked");
        labels.Count(text => text?.EndsWith(" untracked", StringComparison.Ordinal) == true)
            .Should().Be(1, "only Monday has recorded activity");
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenADayHasAGap_ShouldShowABookButtonOnTheGapBand()
    {
        var view = CalendarTestSupport.Build(
            [CalendarTestSupport.Session(MondayNoon.Date.AddHours(9), "Report")],
            activeSpans: day => day == Monday
                ? [new ActiveSpan(MondayNoon.Date.AddHours(9), MondayNoon.Date.AddHours(12))]
                : [],
            now: () => MondayNoon).View;

        Realize(view);

        var book = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Book…");
        ToolTip.GetTip(book).Should().Be("⚠ 10:00–12:00 untracked (2:00)");
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenADayHasUntrackedTime_ShouldShowAnEnabledDistributeButton()
    {
        var view = CalendarTestSupport.Build(
            [CalendarTestSupport.Session(MondayNoon.Date.AddHours(9), "Report")],
            activeTime: day => day == Monday ? TimeSpan.FromMinutes(150) : TimeSpan.Zero,
            now: () => MondayNoon).View;

        Realize(view);

        var distribute = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Distribute");
        distribute.IsEnabled.Should().BeTrue("the day has untracked time a task can absorb");
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenADayIsFullyBooked_ShouldDisableTheDistributeButton()
    {
        var view = CalendarTestSupport.Build(
            [CalendarTestSupport.Session(MondayNoon.Date.AddHours(9), "Report")],
            activeTime: _ => TimeSpan.Zero,
            now: () => MondayNoon).View;

        Realize(view);

        var distribute = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Distribute");
        distribute.IsEnabled.Should().BeFalse("there is no untracked time to distribute");
    }

    private static void Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 500 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
