using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.ActivityMonitor.Tests.Unit;

[TestFixture]
public sealed class WindowsIdleTimeProviderTests
{
    [Test]
    public void IdleMilliseconds_WhenMeasuringIdleTime_ShouldUse32BitTickDifference()
    {
        // Normal case: 90 seconds between now and the last input.
        var current = unchecked((int)1_000_000u);
        var lastInput = unchecked((uint)(1_000_000 - 90_000));
        WindowsIdleTimeProvider.IdleMilliseconds(lastInput, current).Should().Be(90_000);

        // Tick counter wrapped past 2^32 (about every 49.7 days). The last input was
        // "before" the wrap, so a naive 64-bit subtraction would report ~49 days.
        uint wrappedLastInput = uint.MaxValue - 4_999;
        var wrappedCurrent = unchecked((int)5_000u);
        var idle = WindowsIdleTimeProvider.IdleMilliseconds(wrappedLastInput, wrappedCurrent);
        idle.Should().Be(10_000, "the 32-bit difference stays correct across the wrap");
        TimeSpan.FromMilliseconds(idle).Should().Be(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void TryGetLastInputTick_WhenQueried_ShouldReportLastInputTick()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("GetLastInputInfo is a Win32 API");
        }

        // Exercises the real GetLastInputInfo binding. Its cbSize must be filled in,
        // otherwise the call fails and this would report zero idle forever.
        WindowsIdleTimeProvider.TryGetLastInputTick(out var tick).Should().BeTrue(
            "GetLastInputInfo succeeds when cbSize is set");

        var idle = WindowsIdleTimeProvider.IdleMilliseconds(tick, Environment.TickCount);
        idle.Should().BeLessThan(uint.MaxValue / 2,
            "the last input tick is a current value, not a stale or unset one");
    }
}
