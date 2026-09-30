using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Tests.UI;

/// <summary>
/// The day row of the week tree shows the PC active time that no tracked session
/// covers, next to the day's booked total and the PC activity line.
/// </summary>
public sealed class WeekUntrackedTimeTests
{
    [AvaloniaTest]
    public void WeekTrackingTree_WhenActiveTimeExceedsTheBookedTime_ShouldShowTheUntrackedTimeInTheDayRow()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
        var view = WeekViewTestSupport.Build(
            [WeekViewTestSupport.Session(DateTimeOffset.Now.Date.AddHours(9), "Report")],
            activeTime: day => day == today ? TimeSpan.FromMinutes(150) : TimeSpan.Zero).View;

        Realize(view);

        var labels = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        labels.Should().Contain("⚠ 1:30 untracked");
        labels.Count(text => text?.EndsWith(" untracked", StringComparison.Ordinal) == true)
            .Should().Be(1, "only today has recorded activity");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenADayWithAGapIsExpanded_ShouldShowTheGapRowWithABookButton()
    {
        var midnight = DateTimeOffset.Now.Date;
        var offset = DateTimeOffset.Now.Offset;
        var (view, week) = WeekViewTestSupport.Build(
            [WeekViewTestSupport.Session(new DateTimeOffset(midnight.AddHours(9), offset), "Report")],
            activeSpans: day => day == DateOnly.FromDateTime(midnight)
                ? [new ActiveSpan(new DateTimeOffset(midnight.AddHours(9), offset), new DateTimeOffset(midnight.AddHours(12), offset))]
                : []);
        week.Days.Single(d => d.IsToday).IsExpanded = true;

        Realize(view);

        var gapRow = view.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "⚠ 10:00–12:00 untracked (2:00)");
        var buttons = gapRow.Parent.Should().BeOfType<StackPanel>().Which.Children.OfType<Button>().ToList();
        buttons.Should().ContainSingle(b => b.Content as string == "Book…");
        buttons.Should().NotContain(b => b.Content as string == "Distribute",
            "distribute moved to the day row");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenADayHasUntrackedTime_ShouldShowAnEnabledDistributeButtonInTheDayRow()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
        var view = WeekViewTestSupport.Build(
            [WeekViewTestSupport.Session(DateTimeOffset.Now.Date.AddHours(9), "Report")],
            activeTime: day => day == today ? TimeSpan.FromMinutes(150) : TimeSpan.Zero).View;

        Realize(view);

        var distribute = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Distribute");
        distribute.IsEnabled.Should().BeTrue("the day has untracked time a task can absorb");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenADayIsFullyBooked_ShouldDisableTheDistributeButton()
    {
        var view = WeekViewTestSupport.Build(
            [WeekViewTestSupport.Session(DateTimeOffset.Now.Date.AddHours(9), "Report")],
            activeTime: _ => TimeSpan.Zero).View;

        Realize(view);

        var distribute = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Distribute");
        distribute.IsEnabled.Should().BeFalse("there is no untracked time to distribute");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenADayHasBookings_ShouldOfferRoundingOnlyWhenATaskIsOffTheHalfHour()
    {
        var today = DateTimeOffset.Now.Date;
        var offset = DateTimeOffset.Now.Offset;
        var yesterdayOrTomorrow = today.DayOfWeek == DayOfWeek.Monday ? today.AddDays(1) : today.AddDays(-1);
        var view = WeekViewTestSupport.Build(
        [
            WeekViewTestSupport.Session(new DateTimeOffset(today.AddHours(9), offset), "Report", minutes: 50),
            WeekViewTestSupport.Session(new DateTimeOffset(yesterdayOrTomorrow.AddHours(9), offset), "Review", minutes: 90),
        ]).View;

        Realize(view);

        var roundButtons = view.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Content as string == "Round ½h")
            .ToList();
        roundButtons.Should().HaveCount(2, "only the two days with bookings offer rounding");
        roundButtons.Count(b => b.IsEnabled).Should().Be(1, "only the 0:50 day is off the half hour");
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
