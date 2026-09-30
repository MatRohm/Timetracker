using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Tests.Unit;

/// <summary>
/// The week view registers as a tab between the tracker and the options tab.
/// </summary>
[TestFixture]
public sealed class WeekTabContributorTests
{
    [Test]
    public void TabKey_WhenQueried_ShouldBeTheWeekViewKey()
    {
        var contributor = new WeekTabContributor(new WeekViewModel(new FakeTrackedSessions(), [], []));

        contributor.TabKey.Should().Be(TabKeys.WeekView);
    }

    [Test]
    public void Order_WhenQueried_ShouldPlaceItBetweenTrackerAndOptions()
    {
        var contributor = new WeekTabContributor(new WeekViewModel(new FakeTrackedSessions(), [], []));

        contributor.Order.Should().Be(1);
    }
}
