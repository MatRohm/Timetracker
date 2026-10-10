using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.WeekView.Tests.Unit;

/// <summary>
/// In-memory <see cref="IDayActivityQuery"/>: the per-day active time and active
/// spans are set as delegates.
/// </summary>
public sealed class FakeDayActivitySource : IDayActivityQuery
{
    public Func<DateOnly, TimeSpan> ActiveTime { get; set; } = _ => TimeSpan.Zero;

    public Func<DateOnly, IReadOnlyList<ActiveSpan>> ActiveSpans { get; set; } = _ => [];

    public TimeSpan GetActiveTime(DateOnly day) => ActiveTime(day);

    public IReadOnlyList<ActiveSpan> GetActiveSpans(DateOnly day) => ActiveSpans(day);
}
