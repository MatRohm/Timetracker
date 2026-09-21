using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Plugins.Tests.Unit;

/// <summary>
/// Covers the static <see cref="UiHostAccessor"/> bridge: the tab views register
/// their host/sink, and add-ins resolve them lazily. State is process-wide, so
/// each test re-registers its own values instead of relying on a clean slate.
/// </summary>
public sealed class UiHostAccessorTests
{
    [Test]
    public void GetTrackerHost_returns_the_registered_host()
    {
        var host = new FakeHost();
        UiHostAccessor.RegisterTrackerHost(host);

        var resolved = UiHostAccessor.GetTrackerHost<FakeHost>();

        resolved.Should().BeSameAs(host);
    }

    [Test]
    public void GetTrackerHost_throws_for_another_type()
    {
        UiHostAccessor.RegisterTrackerHost(new FakeHost());

        var act = () => UiHostAccessor.GetTrackerHost<string>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*has not registered*");
    }

    [Test]
    public void RegisterTrackerHost_rejects_null()
    {
        var act = () => UiHostAccessor.RegisterTrackerHost(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void RegisterWeekStatusSink_rejects_null()
    {
        var act = () => UiHostAccessor.RegisterWeekStatusSink(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ShowWeekStatus_forwards_to_the_registered_sink()
    {
        string? message = null;
        WeekStatusKind? kind = null;
        UiHostAccessor.RegisterWeekStatusSink((m, k) =>
        {
            message = m;
            kind = k;
        });

        UiHostAccessor.ShowWeekStatus("done", WeekStatusKind.Success);

        message.Should().Be("done");
        kind.Should().Be(WeekStatusKind.Success);
    }

    [Test]
    public void ShowWeekStatus_without_a_sink_does_not_throw()
    {
        // No registration in this test; the accessor must swallow the call.
        var act = () => UiHostAccessor.ShowWeekStatus("ignored", WeekStatusKind.Info);

        act.Should().NotThrow();
    }

    private sealed class FakeHost
    {
    }
}
