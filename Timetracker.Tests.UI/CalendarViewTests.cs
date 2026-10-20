using System.Text.RegularExpressions;
using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Calendar.Views;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.Tests.UI;

/// <summary>
/// The Workweek/Week time grid and the month grid: column count, block geometry,
/// activity strip, now line and month cells.
/// </summary>
public sealed class CalendarViewTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset MondayNoon = new(2026, 9, 21, 12, 0, 0, Offset);
    private static readonly DateTimeOffset OctoberNoon = new(2026, 10, 19, 12, 0, 0, Offset);
    private static readonly Regex DayHeaderPattern = new(@"^(Mon|Tue|Wed|Thu|Fri|Sat|Sun) \d{2}\.\d{2}\.$");

    [AvaloniaTest]
    public void CalendarWeekView_WhenInWorkweekMode_ShouldShowFiveColumns()
    {
        var (view, calendar) = Build(storedMode: nameof(CalendarViewMode.Workweek));

        Realize(view);

        calendar.VisibleDays.Should().HaveCount(5);
        DayHeaders(view).Should().HaveCount(5, "Saturday and Sunday are hidden");
        DayHeaders(view).Should().OnlyContain(text => !text.StartsWith("Sat") && !text.StartsWith("Sun"));
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenInWeekMode_ShouldShowSevenColumns()
    {
        var (view, _) = Build(storedMode: nameof(CalendarViewMode.Week));

        Realize(view);

        DayHeaders(view).Should().HaveCount(7);
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenABlockIsRendered_ShouldMatchTheTimeAxis()
    {
        var (view, _) = Build(sessions: [Session(MondayNoon.Date.AddHours(9), 60, "Report")]);

        Realize(view);

        var block = BlockFor(view, "Report");
        Canvas.GetTop(block).Should().BeApproximately(
            (9 - 6.5) * CalendarLayout.PixelsPerHour,
            1,
            "the axis is 06:30-14:30 and the block starts at 09:00");
        block.Bounds.Height.Should().BeApproximately(CalendarLayout.PixelsPerHour, 0.5);
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenBlocksOverlap_ShouldPlaceThemSideBySide()
    {
        var (view, _) = Build(sessions:
        [
            Session(MondayNoon.Date.AddHours(9), 60, "Report"),
            Session(MondayNoon.Date.AddHours(9).AddMinutes(30), 60, "Review"),
        ]);

        Realize(view);

        var report = BlockFor(view, "Report");
        var review = BlockFor(view, "Review");

        Canvas.GetLeft(review).Should().BeGreaterThan(Canvas.GetLeft(report), "the blocks share the column");
        report.Bounds.Width.Should().BeLessThan(300, "each lane is about half the column");
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenActivitySpansAreRecorded_ShouldDrawAGreenStripPerSpan()
    {
        var (view, _) = Build(
            sessions: [Session(MondayNoon.Date.AddHours(9), 60, "Report")],
            activeSpans: _ => [new(MondayNoon.Date.AddHours(9), MondayNoon.Date.AddHours(10))]);

        Realize(view);

        var strip = view.GetVisualDescendants().OfType<Rectangle>()
            .Where(r => r.Width == 3)
            .ToList();
        strip.Should().ContainSingle("one active span is shown as one green strip segment");
        strip[0].Fill.Should().BeSameAs(ViewBrushes.Success);
    }

    [AvaloniaTest]
    public void CalendarWeekView_WhenTodayIsVisible_ShouldDrawTheNowLine()
    {
        var (view, _) = Build(sessions: [Session(MondayNoon.Date.AddHours(9), 60, "Report")]);

        Realize(view);

        var nowLine = view.GetVisualDescendants().OfType<Rectangle>()
            .Single(r => r.Fill == ViewBrushes.Error && r.Height == 2);
        Canvas.GetTop(nowLine).Should().BeApproximately(
            (12 - 6.5) * CalendarLayout.PixelsPerHour,
            1,
            "the now line sits at the current time");
    }

    [AvaloniaTest]
    public void CalendarMonthView_WhenInMonthMode_ShouldShowTheMonthCellsAndChips()
    {
        var (view, calendar) = Build(
            storedMode: nameof(CalendarViewMode.Month),
            sessions:
            [
                Session(new DateTimeOffset(2026, 10, 5, 9, 0, 0, Offset), 60, "Report"),
                Session(new DateTimeOffset(2026, 10, 5, 10, 0, 0, Offset), 60, "Review"),
                Session(new DateTimeOffset(2026, 10, 5, 11, 0, 0, Offset), 60, "Deploy"),
                Session(new DateTimeOffset(2026, 10, 5, 12, 0, 0, Offset), 60, "Meeting"),
            ],
            now: () => OctoberNoon);

        Realize(view);

        calendar.Mode.Should().Be(CalendarViewMode.Month);
        MonthCells(view).Should().HaveCount(MonthGridBuilder.CellCount);
        var texts = Texts(view).Select(t => t.Text ?? "").ToList();
        texts.Should().Contain("October 2026");
        texts.Should().Contain("+1 more");
    }

    [AvaloniaTest]
    public void CalendarMonthView_WhenACellIsClicked_ShouldOpenThatWeekInWeekMode()
    {
        var (view, calendar) = Build(storedMode: nameof(CalendarViewMode.Month), now: () => OctoberNoon);

        Realize(view);

        var day = Texts(view).Single(t => t.Text == "14");
        var button = day.GetVisualAncestors().OfType<Button>().First();
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        calendar.Mode.Should().Be(CalendarViewMode.Week);
        calendar.Days[0].Date.Date.Should().Be(new DateTime(2026, 10, 12));
        Texts(view).Select(t => t.Text ?? "").Should().Contain(t => t.StartsWith("Week "));
    }

    [AvaloniaTest]
    public void CalendarTabView_WhenTheModeButtonIsClicked_ShouldSwitchModes()
    {
        var (view, calendar) = Build(storedMode: nameof(CalendarViewMode.Workweek));

        Realize(view);

        var month = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Month");
        month.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        calendar.Mode.Should().Be(CalendarViewMode.Month);
        MonthCells(view).Should().HaveCount(MonthGridBuilder.CellCount);
    }

    private static (CalendarTabView View, CalendarViewModel Calendar) Build(
        string storedMode = nameof(CalendarViewMode.Week),
        TrackedSession[]? sessions = null,
        Func<DateOnly, IReadOnlyList<ActiveSpan>>? activeSpans = null,
        Func<DateTimeOffset>? now = null) =>
        CalendarTestSupport.Build(
            sessions ?? [],
            activeSpans: activeSpans,
            storedMode: storedMode,
            now: now ?? (() => MondayNoon));

    private static TrackedSession Session(DateTimeOffset start, int minutes, string task) =>
        CalendarTestSupport.Session(start, task, minutes: minutes);

    private static Window Realize(Control root)
    {
        var host = new Window { Content = root, Width = 1100, Height = 700 };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return host;
    }

    private static IReadOnlyList<TextBlock> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()];

    private static IReadOnlyList<string> DayHeaders(Control root) =>
        [.. Texts(root).Select(t => t.Text ?? "")
            .Where(text => DayHeaderPattern.IsMatch(text))];

    private static Border BlockFor(Control root, string task)
    {
        var label = Texts(root).First(t => t.Text == task);
        var block = label.GetVisualAncestors().OfType<Border>()
            .First(b => b.Child is Grid);
        return block;
    }

    private static IReadOnlyList<Button> MonthCells(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(b => b.Content is StackPanel)];
}
