using Avalonia.Controls;
using Timetracker.Plugins.Calendar.Tests.Unit;
using Timetracker.Plugins.Calendar.ViewModels;
using Timetracker.Plugins.Calendar.Views;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Tests.UI;

/// <summary>
/// Builds the calendar (its tab and view model) from plain sessions, wiring the
/// fakes the UI tests need instead of a full application container. Defaults to
/// Week mode so all seven days, including the current one on a weekend, are
/// rendered; pass another stored mode for mode-specific tests.
/// </summary>
internal static class CalendarTestSupport
{
    public static (CalendarTabView View, CalendarViewModel Calendar) Build(
        IEnumerable<TrackedSession> sessions,
        Func<DateOnly, TimeSpan>? activeTime = null,
        Func<DateOnly, IReadOnlyList<ActiveSpan>>? activeSpans = null,
        IReadOnlyList<Control>? contributors = null,
        string? storedMode = nameof(CalendarViewMode.Week),
        Func<DateTimeOffset>? now = null)
    {
        var activity = new FakeDayActivitySource
        {
            ActiveTime = activeTime ?? (_ => TimeSpan.Zero),
            ActiveSpans = activeSpans ?? (_ => []),
        };
        var tracked = new FakeTrackedSessions();
        var options = new FakeOptions();
        if (storedMode is not null)
        {
            options.Set(CalendarOptions.ViewModeKey, storedMode);
        }

        var calendar = new CalendarViewModel(tracked, tracked, [], [activity], options, options, now);
        tracked.SetSessions([.. sessions]);
        return (new CalendarTabView(calendar, contributors ?? []), calendar);
    }

    /// <summary>A session starting at <paramref name="start"/>.</summary>
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
