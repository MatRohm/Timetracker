using AwesomeAssertions;
using NUnit.Framework;
using static Timetracker.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class MonitorLogTests
{
    [Test]
    public void DefaultFilePath_WhenCreated_ShouldPointNextToTheExecutable()
    {
        MonitorLog.DefaultFilePath.Should().Be(
            Path.Combine(AppContext.BaseDirectory, "Timetracker.ActivityMonitor.log"));
    }

    [Test]
    public void Info_WhenEntryIsWritten_ShouldTagItWithCallingMethod()
    {
        var path = TempPath($"monitor-method-{Guid.NewGuid():N}.log");
        var monitorLog = new MonitorLog(path);

        WriteEntry(monitorLog, "hello");

        var text = File.ReadAllText(path);
        text.Should().Contain("[WriteEntry]", "the entry is tagged with the calling method");
        text.Should().Contain("hello");
    }

    private static void WriteEntry(MonitorLog monitorLog, string message) =>
        monitorLog.Info(message);
}
