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
        vm.Rows.Should().OnlyContain(r => !r.IsBlocked);
        vm.Remaining.Should().Be(TimeSpan.Zero);
        vm.RemainingText.Should().Be("left 0:00");
        vm.MissingText.Should().Be("1:15 untracked");
        vm.Rows[0].ChangeText.Should().Be("09:00–10:00 → 09:00–10:45");
    }

    [Test]
    public void Rows_WhenAShareCannotBePlaced_ShouldStartThatRowAtZeroWithTheBlockedReason()
    {
        // Report's 09:00–10:00 sits between Meeting 08:30–09:00 and Deploy 10:00–11:00,
        // so its 15-minute share cannot extend either way.
        var vm = Create(TimeSpan.FromHours(4),
            Session("Meeting", At(8, 30), At(9, 0)),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Review", At(12, 0), At(12, 30)),
            Session("Deploy", At(10, 0), At(11, 0)));

        var report = vm.Rows.Single(r => r.Task == "Report");
        report.IsBlocked.Should().BeTrue();
        report.BlockedText.Should().Be("no free time next to its last session");
        report.ShareText.Should().BeEmpty();
        report.TargetText.Should().BeEmpty();
        report.IncreaseCommand.CanExecute(null).Should().BeFalse("even +0:15 cannot be placed");
        vm.Plan.Tasks.Should().NotContain(t => t.Name == "Report");
        vm.Rows.Single(r => r.Task == "Deploy").ShareText.Should().Be("+0:15", "the other rows carry their prefill");
        vm.CanAccept.Should().BeTrue("the other rows still carry shares");
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
    public void Increase_WhenAHandBackFreesTime_ShouldStayBlockedOnlyForTheBoxedRow()
    {
        // Report's +0:15 was blocked at prefill (walled in by Meeting and Deploy);
        // draining Deploy's prefill grows the pool, but Report stays blocked while
        // Deploy itself can grow into the freed eleven o'clock slot.
        var vm = Create(TimeSpan.FromHours(4),
            Session("Meeting", At(8, 30), At(9, 0)),
            Session("Report", At(9, 0), At(10, 0)),
            Session("Deploy", At(10, 0), At(11, 0)));
        vm.Rows.Single(r => r.Task == "Report").IsBlocked.Should().BeTrue();

        vm.Decrease(vm.Rows.Single(r => r.Task == "Deploy"));

        vm.Rows.Single(r => r.Task == "Report").CanIncrease.Should().BeFalse("the walls did not move");
        vm.Rows.Single(r => r.Task == "Deploy").CanIncrease.Should().BeTrue("Deploy can grow to 11:15");
        vm.Increase(vm.Rows.Single(r => r.Task == "Deploy"));
        vm.Rows.Single(r => r.Task == "Deploy").ShareText.Should().Be("+0:45");
        vm.Remaining.Should().Be(TimeSpan.FromMinutes(30));
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
        var result = new DistributeDayViewModel("Mon 21.09.", missing, daySessions, sessions, null);
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

