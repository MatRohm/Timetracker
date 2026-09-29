using AwesomeAssertions;
using NUnit.Framework;
using static Timetracker.Plugins.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class ActivityWeekDayContributorTests
{
    private static readonly DateOnly Monday = new(2026, 9, 21);

    [Test]
    public void GetDayText_WhenActiveAndIdleSpansAreLogged_ShouldShowBothTotals()
    {
        var contributor = Contributor(out var log);
        log.Add("active", At(9, 0), At(12, 0));
        log.Add("idle", At(12, 0), At(13, 30));
        log.Add("active", At(13, 30), At(15, 45));

        contributor.GetDayText(Monday).Should().Be("PC 5:15 active · 1:30 idle");
    }

    [Test]
    public void GetActiveTime_WhenActiveAndIdleSpansAreLogged_ShouldSumOnlyTheActiveSpans()
    {
        var contributor = Contributor(out var log);
        log.Add("active", At(9, 0), At(12, 0));
        log.Add("idle", At(12, 0), At(13, 30));
        log.Add("active", At(13, 30), At(15, 45));

        contributor.GetActiveTime(Monday).Should().Be(TimeSpan.FromMinutes(315));
    }

    [Test]
    public void GetActiveTime_WhenASpanCrossesMidnight_ShouldCountOnlyThatDaysPart()
    {
        var contributor = Contributor(out var log);
        var offset = TimeSpan.FromHours(2);
        log.Add("active",
            new DateTimeOffset(2026, 9, 21, 23, 0, 0, offset),
            new DateTimeOffset(2026, 9, 22, 1, 30, 0, offset));

        contributor.GetActiveTime(Monday).Should().Be(TimeSpan.FromHours(1));
        contributor.GetActiveTime(Monday.AddDays(1)).Should().Be(TimeSpan.FromMinutes(90));
    }

    [Test]
    public void GetActiveTime_WhenNothingWasLoggedThatDay_ShouldBeZero()
    {
        var contributor = Contributor(out var log);
        log.Add("active", At(9, 0), At(12, 0));

        contributor.GetActiveTime(Monday.AddDays(1)).Should().Be(TimeSpan.Zero);
    }

    private static ActivityWeekDayContributor Contributor(out ActivityLog log)
    {
        var path = TempPath($"contributor-{Guid.NewGuid():N}.json");
        log = new ActivityLog(path);
        var result = new ActivityWeekDayContributor(log);
        return result;
    }
}
