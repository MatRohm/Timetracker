using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Tests.Unit;

/// <summary>
/// The calendar registers as a tab between the tracker and the options tab.
/// </summary>
[TestFixture]
public sealed class CalendarTabContributorTests
{
    [Test]
    public void TabKey_WhenQueried_ShouldBeTheCalendarKey()
    {
        var contributor = new CalendarTabContributor(new CalendarViewModel(new FakeTrackedSessions(), new FakeTrackedSessions(), [], [], new FakeOptions(), new FakeOptions()));

        contributor.TabKey.Should().Be(TabKeys.Calendar);
    }

    [Test]
    public void Order_WhenQueried_ShouldPlaceItBetweenTrackerAndOptions()
    {
        var contributor = new CalendarTabContributor(new CalendarViewModel(new FakeTrackedSessions(), new FakeTrackedSessions(), [], [], new FakeOptions(), new FakeOptions()));

        contributor.Order.Should().Be(1);
    }
}
