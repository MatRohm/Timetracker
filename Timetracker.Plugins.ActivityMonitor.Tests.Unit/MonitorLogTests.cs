using AwesomeAssertions;
using NUnit.Framework;
using static Timetracker.Plugins.ActivityMonitor.Tests.Unit.TestSupport;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class MonitorLogTests
{
    [Test]
    public void DefaultFilePath_WhenCreated_ShouldPointNextToTheExecutable()
    {
        MonitorLog.DefaultFilePath.Should().Be(
            Path.Combine(AppContext.BaseDirectory, "Timetracker.Plugins.ActivityMonitor.log"));
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
