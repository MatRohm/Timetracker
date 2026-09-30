using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.Plugins.WeekView.ViewModels;
using Timetracker.Plugins.WeekView.Views;

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

    private static (WeekTabView View, WeekViewModel Week) Build() =>
        WeekViewTestSupport.Build(
            [WeekViewTestSupport.Session(DateTimeOffset.Now.Date.AddHours(9), "Report", "Project X")]);

    private static void Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 500 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
