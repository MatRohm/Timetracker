using AwesomeAssertions;
using NUnit.Framework;
using static Timetracker.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class ActivityLogTests
{
    [Test]
    public void Add_WhenSpansAreAppended_ShouldReturnThemOrderedByFile()
    {
        var path = TempPath("log-append.json");
        File.Delete(path);
        var log = new ActivityLog(path);

        log.Add("active", At(9, 0), At(12, 0));
        log.Add("idle", At(12, 0), At(13, 30));

        var spans = log.GetAll();
        spans.Should().HaveCount(2);
        spans[0].Kind.Should().Be("active");
        spans[0].DurationText.Should().Be("3:00");
        spans[1].Kind.Should().Be("idle");
        spans[1].DurationText.Should().Be("1:30");
    }

    [Test]
    public void GetAll_WhenFileIsMissing_ShouldReturnAnEmptyList()
    {
        var log = new ActivityLog(TempPath("does-not-exist.json"));

        log.GetAll().Should().BeEmpty();
    }
}
