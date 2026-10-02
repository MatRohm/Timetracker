using AwesomeAssertions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// The distribute dialog: one stepper line per task of the day, −/+ moving
/// fifteen minutes between the rows, and a Distribute button that hands the
/// plan's changes to the caller.
/// </summary>
public sealed class DistributeDayDialogTests
{
    [AvaloniaTest]
    public void DistributeDayWindow_WhenRendered_ShouldShowOneStepperLinePerTask()
    {
        var dialog = Build(
            TimeSpan.FromMinutes(135),
            WeekViewTestSupport.Session(At(9, 0), "Report"),
            WeekViewTestSupport.Session(At(12, 0), "Review", minutes: 30));

        var texts = DialogTexts(dialog);
        // The row shows "Now → target" (e.g. "1:00 → 1:30"); the prefill itself is
        // not printed as a share. Steppers and the summary line must be there.
        texts.Should().Contain("1:00");
        texts.Should().Contain("1:30");
        texts.Should().Contain("0:45");
        texts.Should().Contain("left 0:00");
        DialogButtons(dialog).Where(b => (string)(b.Content ?? "") is "−" or "+").Should().HaveCount(4, "two steppers per task row");
    }

    [AvaloniaTest]
    public void DistributeDayWindow_WhenMinusThenPlusClicked_ShouldMoveTheFifteenMinutesBetweenRows()
    {
        var dialog = Build(
            TimeSpan.FromMinutes(135),
            WeekViewTestSupport.Session(At(9, 0), "Report"),
            WeekViewTestSupport.Session(At(12, 0), "Review", minutes: 30));

        // Report's prefill is +1:00: hand 15 back and give it to Review.
        var reviewRow = dialog.ViewModel.Rows.Single(r => r.Task == "Review");
        var reportRow = dialog.ViewModel.Rows.Single(r => r.Task == "Report");
        reportRow.DecreaseCommand.Execute(null);
        reviewRow.IncreaseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // Report hands 15 back (+0:30 → +0:15), Review takes it (+0:15 → +0:30).
        reportRow.ShareText.Should().Be("+0:15");
        reviewRow.ShareText.Should().Be("+0:30");
        dialog.ViewModel.Remaining.Should().Be(TimeSpan.Zero);
        var texts = DialogTexts(dialog);
        texts.Should().Contain("1:15");
        texts.Should().Contain("1:00");
        texts.Should().Contain("left 0:00");
    }

    [AvaloniaTest]
    public void DistributeDayWindow_WhenADaysShareCannotBePlaced_ShouldShowTheReasonAndDisableItsPlus()
    {
        // Report 09:00–10:00 is boxed in by Meeting 08:30–09:00 and Deploy 10:00–11:00.
        var dialog = Build(
            TimeSpan.FromHours(4),
            WeekViewTestSupport.Session(At(8, 30), "Meeting", minutes: 30),
            WeekViewTestSupport.Session(At(9, 0), "Report"),
            WeekViewTestSupport.Session(At(10, 0), "Deploy"));

        var reportRow = dialog.ViewModel.Rows.Single(r => r.Task == "Report");
        reportRow.IsBlocked.Should().BeTrue();
        reportRow.BlockedText.Should().Be("no free time next to its last session");
        dialog.ViewModel.Plan.Tasks.Should().NotContain(t => t.Name == "Report");
        reportRow.IncreaseCommand.CanExecute(null).Should().BeFalse();
    }

    [AvaloniaTest]
    public void DistributeDayWindow_WhenDistributeClicked_ShouldSetSavedAndCarryThePlansChanges()
    {
        var dialog = Build(
            TimeSpan.FromMinutes(135),
            WeekViewTestSupport.Session(At(9, 0), "Report"),
            WeekViewTestSupport.Session(At(12, 0), "Review", minutes: 30));

        dialog.ViewModel.Rows.Single(r => r.Task == "Review").DecreaseCommand.Execute(null);
        var distribute = FindButtonsByContent(dialog, "Distribute").Single();
        distribute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        dialog.Saved.Should().BeTrue();
        // Review's step went back to the pool: Report's prefill stays +0:30 → 10:30.
        dialog.ViewModel.Plan.Changes.Should().ContainSingle().Which.Original.Task.Should().Be("Report");
        dialog.ViewModel.Plan.Changes.Single().Updated!.End.Should().Be(At(10, 30));
    }

    private static DistributeDayWindow Build(TimeSpan active, params TrackedSession[] sessions)
    {
        var (view, week) = WeekViewTestSupport.Build(
            sessions,
            activeTime: day => day == DateOnly.FromDateTime(DateTimeOffset.Now.Date) ? active : TimeSpan.Zero);
        _ = view;
        var day = week.Days.Single(d => d.IsToday);
        var result = new DistributeDayWindow(week.CreateDistributeDay(day));
        Realize(result);
        return result;
    }

    private static IReadOnlyList<string> DialogTexts(Window dialog) =>
        [.. dialog.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0)];

    private static IReadOnlyList<Button> DialogButtons(Window dialog) =>
        [.. dialog.GetVisualDescendants().OfType<Button>()];

    private static IReadOnlyList<Button> FindButtonsByContent(Window dialog, string content) =>
        [.. DialogButtons(dialog).Where(b => (string)(b.Content ?? "") == content)];

    private static DateTimeOffset At(int hour, int minute) =>
        new(DateTimeOffset.Now.Date.AddHours(hour).AddMinutes(minute), DateTimeOffset.Now.Offset);

    /// <summary>Shows the dialog so its visual tree is built and laid out.</summary>
    private static void Realize(Window window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
