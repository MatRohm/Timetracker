using Avalonia.Controls;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Tests.Unit;
using Timetracker.Plugins.WeekView.ViewModels;
using Timetracker.Plugins.WeekView.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// Builds the week view (its tab and view model) from plain sessions, wiring the
/// fakes the UI tests need instead of a full application container.
/// </summary>
internal static class WeekViewTestSupport
{
    public static (WeekTabView View, WeekViewModel Week) Build(
        IEnumerable<TrackedSession> sessions,
        Func<DateOnly, TimeSpan>? activeTime = null,
        Func<DateOnly, IReadOnlyList<ActiveSpan>>? activeSpans = null,
        IReadOnlyList<Control>? contributors = null)
    {
        var activity = new FakeDayActivitySource
        {
            ActiveTime = activeTime ?? (_ => TimeSpan.Zero),
            ActiveSpans = activeSpans ?? (_ => []),
        };
        var tracked = new FakeTrackedSessions();
        var week = new WeekViewModel(tracked, tracked, [], [activity]);
        tracked.SetSessions([.. sessions]);
        return (new WeekTabView(week, contributors ?? []), week);
    }

    /// <summary>A one-hour session starting at <paramref name="start"/>.</summary>
    public static TrackedSession Session(
        DateTimeOffset start, string task, string bookingElement = "", int minutes = 60) => new()
        {
            Task = task,
            BookingElement = bookingElement,
            Start = start,
            End = start.AddMinutes(minutes),
            Duration = TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"),
            DurationSeconds = minutes * 60.0,
        };
}
