using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.App.Models;
using Timetracker.App.Tests.Unit;
using Timetracker.App.ViewModels;
using Timetracker.App.Views;

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
        var (repo, _) = RepositoryFake.Create();
        using var tracker = new TrackerViewModel(TrackerDependenciesFactory.Create(repo));
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
        tracker.Week.DayActiveTime = day => day == today ? TimeSpan.FromMinutes(150) : TimeSpan.Zero;
        tracker.Week.UpdateSessions(
        [
            new TrackerEntry
            {
                Task = "Report",
                Start = DateTimeOffset.Now.Date.AddHours(9),
                End = DateTimeOffset.Now.Date.AddHours(10),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            },
        ]);
        var view = new WeekTabView(tracker.Week, []);

        Realize(view);

        var labels = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        labels.Should().Contain("⚠ 1:30 untracked");
        labels.Count(text => text?.EndsWith(" untracked", StringComparison.Ordinal) == true)
            .Should().Be(1, "only today has recorded activity");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenADayWithAGapIsExpanded_ShouldShowTheGapRowWithABookButton()
    {
        var (repo, _) = RepositoryFake.Create();
        using var tracker = new TrackerViewModel(TrackerDependenciesFactory.Create(repo));
        var midnight = DateTimeOffset.Now.Date;
        var offset = DateTimeOffset.Now.Offset;
        tracker.Week.DayActiveSpans = day => day == DateOnly.FromDateTime(midnight)
            ? [new TimeRange(new DateTimeOffset(midnight.AddHours(9), offset), new DateTimeOffset(midnight.AddHours(12), offset))]
            : [];
        tracker.Week.UpdateSessions(
        [
            new TrackerEntry
            {
                Task = "Report",
                Start = new DateTimeOffset(midnight.AddHours(9), offset),
                End = new DateTimeOffset(midnight.AddHours(10), offset),
                Duration = "01:00:00",
                DurationSeconds = 3600,
            },
        ]);
        tracker.Week.Days.Single(d => d.IsToday).IsExpanded = true;
        var view = new WeekTabView(tracker.Week, []);

        Realize(view);

        var gapRow = view.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "⚠ 10:00–12:00 untracked (2:00)");
        var buttons = gapRow.Parent.Should().BeOfType<StackPanel>().Which.Children.OfType<Button>().ToList();
        buttons.Should().ContainSingle(b => b.Content as string == "Book…");
        buttons.Should().ContainSingle(b => b.Content as string == "Distribute")
            .Which.IsEnabled.Should().BeTrue("the session 09:00–10:00 borders the gap");
    }

    [AvaloniaTest]
    public void WeekTrackingTree_WhenNoSessionBordersAGap_ShouldDisableItsDistributeButton()
    {
        var (repo, _) = RepositoryFake.Create();
        using var tracker = new TrackerViewModel(TrackerDependenciesFactory.Create(repo));
        var midnight = DateTimeOffset.Now.Date;
        var offset = DateTimeOffset.Now.Offset;
        tracker.Week.DayActiveSpans = day => day == DateOnly.FromDateTime(midnight)
            ? [new TimeRange(new DateTimeOffset(midnight.AddHours(9), offset), new DateTimeOffset(midnight.AddHours(12), offset))]
            : [];
        tracker.Week.UpdateSessions([]);
        tracker.Week.Days.Single(d => d.IsToday).IsExpanded = true;
        var view = new WeekTabView(tracker.Week, []);

        Realize(view);

        var distribute = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Distribute");
        distribute.IsEnabled.Should().BeFalse("nothing starts or ends at the gap, so nothing can absorb it");
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
