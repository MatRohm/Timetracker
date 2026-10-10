using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Tests.Unit.ViewModels;

[TestFixture]
public sealed class DistributeDayViewModelTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Test]
    public void Rows_WhenCreated_ShouldPrefillProportionalSharesOnTheStepGrid()
    {
        var vm = Create(TimeSpan.FromMinutes(165),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)));

        vm.Rows.Select(r => (r.Task, r.CurrentText, r.ShareText, r.TargetText)).Should().Equal(
            ("Report", "1:00", "+0:45", "1:45"),
            ("Review", "0:30", "+0:30", "1:00"));
        vm.Remaining.Should().Be(TimeSpan.Zero);
        vm.RemainingText.Should().Be("left 0:00");
        vm.MissingText.Should().Be("1:15 untracked");
        vm.Rows[0].ChangeText.Should().Be("09:00–10:00 → 09:00–10:45");
    }

    [Test]
    public void Rows_WhenAShareIsBoxedIn_ShouldStillPlaceIt()
    {
        // Report's 09:00–10:00 sits between Meeting 08:30–09:00 and Deploy 10:00–11:00,
        // but its share still grows the session forward.
        var vm = Create(TimeSpan.FromHours(4),
            Session("Meeting", At(8, 30), At(9, 0)),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)),
            Session("Deploy", At(10, 0), At(11, 0)));

        var report = vm.Rows.Single(r => r.Task == "Report");
        report.ShareText.Should().Be("+0:15");
        report.TargetText.Should().Be("1:15");
        vm.Plan.Tasks.Should().Contain(t => t.Name == "Report");
        vm.CanAccept.Should().BeTrue();
    }

    [Test]
    public void Decrease_WhenTheShareIsPositive_ShouldHandTheStepBackToThePool()
    {
        var vm = Create(TimeSpan.FromMinutes(135),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)));
        var report = vm.Rows.Single(r => r.Task == "Report");

        vm.Decrease(report);

        report.ShareText.Should().Be("+0:15");
        vm.Remaining.Should().Be(TimeSpan.FromMinutes(15));
        vm.Rows.Single(r => r.Task == "Review").ShareText.Should().Be("+0:15", "the other row keeps its share");
    }

    [Test]
    public void Decrease_WhenTheShareIsZero_ShouldDoNothing()
    {
        var vm = Create(TimeSpan.Zero, Session("Report", At(9, 0), At(10, 0)));

        var report = vm.Rows.Single();
        report.DecreaseCommand.CanExecute(null).Should().BeFalse("nothing is assigned yet");
        report.IncreaseCommand.CanExecute(null).Should().BeFalse("there is no untracked time either");

        vm.Decrease(report);

        report.ShareText.Should().BeEmpty("a disabled command changed nothing");
    }

    [Test]
    public void Increase_WhenTheRowMayGrow_ShouldTakeAFreeStepFromThePool()
    {
        var vm = Create(TimeSpan.FromMinutes(135),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)));

        // Hand Review's step back and assign it to Report.
        vm.Decrease(vm.Rows.Single(r => r.Task == "Review"));
        var report = vm.Rows.Single(r => r.Task == "Report");
        vm.Increase(report);

        report.ShareText.Should().Be("+0:45");
        vm.Remaining.Should().Be(TimeSpan.Zero);
        vm.Rows.Single(r => r.Task == "Review").ShareText.Should().BeEmpty();
    }

    [Test]
    public void Increase_WhenNoUntrackedTimeRemains_ShouldKeepEveryRowDisabled()
    {
        var vm = Create(TimeSpan.FromMinutes(135),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)));

        vm.Remaining.Should().Be(TimeSpan.Zero);
        vm.Rows.Should().OnlyContain(r => !r.IncreaseCommand.CanExecute(null), "the whole day's time is assigned");
    }

    [Test]
    public void Increase_WhenATaskIsBoxedIn_ShouldStillAllowItToGrow()
    {
        // Report's 09:00–10:00 is walled in by Meeting and Deploy, but it can still
        // take a step once one is freed from the pool.
        var vm = Create(TimeSpan.FromHours(4),
            Session("Meeting", At(8, 30), At(9, 0)),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Deploy", At(10, 0), At(11, 0)));

        vm.Decrease(vm.Rows.Single(r => r.Task == "Deploy"));
        var report = vm.Rows.Single(r => r.Task == "Report");

        report.IncreaseCommand.CanExecute(null).Should().BeTrue("the boxed-in task can still grow");
        vm.Increase(report);

        report.ShareText.Should().Be("+0:45");
    }

    [Test]
    public void Plan_WhenAdjusted_ShouldCarryExactlyTheAssignedSharesAsSessionChanges()
    {
        var vm = Create(TimeSpan.FromMinutes(135),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)));

        // Hand Review's 15 minutes to Report.
        vm.Decrease(vm.Rows.Single(r => r.Task == "Review"));
        vm.Increase(vm.Rows.Single(r => r.Task == "Report"));

        vm.Plan.Tasks.Select(t => (t.Name, t.Share)).Should().Equal(
            ("Report", TimeSpan.FromMinutes(45)));
        vm.Plan.Changes.Should().HaveCount(1);
        vm.Plan.Changes.Single().Original.Task.Should().Be("Report");
        vm.Plan.Changes.Single().Updated!.End.Should().Be(At(10, 45));
        vm.CanAccept.Should().BeTrue();
    }

    [Test]
    public void Plan_WhenEveryRowIsEmpty_ShouldNotOfferAnythingToAccept()
    {
        var vm = Create(TimeSpan.FromMinutes(135),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)));

        vm.CanAccept.Should().BeTrue("both rows carry the prefill");

        // Hand every assigned minute back; the dialog then has nothing to apply.
        foreach (var row in vm.Rows)
        {
            while (row.DecreaseCommand.CanExecute(null))
            {
                vm.Decrease(row);
            }
        }

        vm.Plan.Changes.Should().BeEmpty();
        vm.Remaining.Should().Be(TimeSpan.FromMinutes(45), "everything was handed back");
        vm.CanAccept.Should().BeFalse();
    }

    private static DistributeDayViewModel Create(TimeSpan active, params TrackedSession[] sessions)
    {
        // Sessions must all sit on the same day, like the tree's day rows.
        var daySessions = sessions.Where(s => s.Start.Date == Date).ToList();
        var booked = TimeSpan.Zero;
        foreach (var session in sessions)
        {
            booked += TimeSpan.FromSeconds(session.DurationSeconds);
        }
        var missing = active - booked;
        if (missing < TimeSpan.Zero)
        {
            missing = TimeSpan.Zero;
        }
        var result = new DistributeDayViewModel("Mon 21.09.", missing, daySessions);
        return result;
    }

    private static DateTime Date => new(2026, 9, 21);

    private static DateTimeOffset At(int hour, int minute) => new(Date.AddHours(hour).AddMinutes(minute), Offset);

    private static TrackedSession Session(string task, DateTimeOffset start, DateTimeOffset end)
    {
        var result = new TrackedSession { Task = task }.Reschedule(start, end);
        return result;
    }
}

