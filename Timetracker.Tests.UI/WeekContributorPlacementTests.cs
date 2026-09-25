using Timetracker.Interfaces;
using Timetracker.Plugins.Interfaces;
using AwesomeAssertions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Timetracker.Plugins;
using Timetracker.Tests.Unit;
using Timetracker.ViewModels;
using Timetracker.Views;
using Timetracker.Views.Components;

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
        using var tracker = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());

        var services = new ServiceCollection();
        services.AddSingleton<ITrackerRepository>(repo);
        services.AddSingleton<IUiTimer, FakeTimer>();
        services.AddSingleton<ITrackerUiHost, FakeHost>();
        services.AddSingleton<IWeekStatusHost>(new FakeWeekStatusHost());
        services.AddSingleton<IUiContributor, FakeWeekContributor>();
        var view = new WeekTabView(tracker.Week, services.BuildServiceProvider());

        Realize(view);

        var control = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content?.ToString() == "Test contributor");

        // The control is grouped into a row that also carries the status line.
        var contributorRow = control.Parent.Should().BeOfType<StackPanel>().Subject;
        var bottomPanel = contributorRow.Parent.Should().BeOfType<StackPanel>().Subject;
        bottomPanel.Children[0].Should().BeSameAs(contributorRow);
        bottomPanel.Children[1].Should().BeOfType<TextBlock>("the status label sits after the controls");

        // It is laid out below the day grid, i.e. at the bottom of the view.
        var daysGrid = view.GetVisualDescendants().OfType<WeekDaysGrid>().Single();
        var controlTop = control.TranslatePoint(default, view)!.Value.Y;
        var gridTop = daysGrid.TranslatePoint(default, view)!.Value.Y;
        controlTop.Should().BeGreaterThan(gridTop, "add-in controls are at the bottom, below the day columns");
    }

    private static void Realize(Control root)
    {
        var host = new Window { Content = root, Width = 900, Height = 500 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class FakeWeekContributor : IUiContributor
    {
        public string TargetTab => "Week view";

        public Control CreateControl(IServiceProvider services) =>
            new Button { Content = "Test contributor" };
    }

    private sealed class FakeRepo : ITrackerRepository
    {
        public string FilePath => "memory.json";
        public IReadOnlyList<Timetracker.Models.TrackerEntry> GetAll() => [];
        public void Add(Timetracker.Models.TrackerEntry entry) { }
        public void Save(IReadOnlyList<Timetracker.Models.TrackerEntry> e) { }
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
