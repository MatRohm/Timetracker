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

    private static void Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 500 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
