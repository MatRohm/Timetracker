using Timetracker.App.Interfaces;
using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using Timetracker.Tests.Unit;
using Timetracker.App.ViewModels;
using Timetracker.App.Views;
using Timetracker.App.Views.Components;

namespace Timetracker.Tests.UI;

/// <summary>
/// Add-in controls contributed to the week view (e.g. the PC activity monitor
/// install/remove buttons) sit at the bottom of the view, directly above the
/// shared status line, instead of in the week navigation toolbar.
/// </summary>
public sealed class WeekContributorPlacementTests
{
    [AvaloniaTest]
    public void WeekTabView_WhenRendered_ShouldPlaceContributorControlsAboveTheStatusLine()
    {
        var repo = new FakeRepo();
        using var tracker = new TrackerViewModel(TrackerDependenciesFactory.Create(repo));

        var view = new WeekTabView(tracker.Week, [new Button { Content = "Test contributor" }]);

        Realize(view);

        var control = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content?.ToString() == "Test contributor");

        // The control is grouped into a row that also carries the status line.
        var contributorRow = control.Parent.Should().BeOfType<StackPanel>().Subject;
        var bottomPanel = contributorRow.Parent.Should().BeOfType<StackPanel>().Subject;
        bottomPanel.Children[0].Should().BeSameAs(contributorRow);
        bottomPanel.Children[1].Should().BeOfType<TextBlock>("the status label sits after the controls");

        // It is laid out below the week tree, i.e. at the bottom of the view.
        var tree = view.GetVisualDescendants().OfType<WeekTrackingTree>().Single();
        var controlTop = control.TranslatePoint(default, view)!.Value.Y;
        var gridTop = tree.TranslatePoint(default, view)!.Value.Y;
        controlTop.Should().BeGreaterThan(gridTop, "add-in controls are at the bottom, below the tree");
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
        public Task<IReadOnlyList<Timetracker.App.Models.TrackerEntry>> GetAllAsync() => Task.FromResult<IReadOnlyList<Timetracker.App.Models.TrackerEntry>>([]);
        public Task AddAsync(Timetracker.App.Models.TrackerEntry entry) => Task.CompletedTask;
        public Task SaveAsync(IReadOnlyList<Timetracker.App.Models.TrackerEntry> e) => Task.CompletedTask;
    }
}
