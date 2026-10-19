using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Calendar.ViewModels;

namespace Timetracker.Plugins.Calendar.Tests.Unit.ViewModels;

[TestFixture]
public sealed class CalendarTimeFormatTests
{
    [Test]
    public void HoursMinutes_WhenTheDurationIsBelowADay_ShouldFormatHoursAndMinutes()
    {
        CalendarTimeFormat.HoursMinutes(TimeSpan.FromMinutes(90)).Should().Be("1:30");
    }

    [Test]
    public void HoursMinutes_WhenTheDurationExceedsADay_ShouldNotWrapAtTwentyFour()
    {
        var value = TimeSpan.FromHours(25) + TimeSpan.FromMinutes(10);

        CalendarTimeFormat.HoursMinutes(value).Should().Be("25:10",
            "week and month totals can exceed a day");
    }
}
