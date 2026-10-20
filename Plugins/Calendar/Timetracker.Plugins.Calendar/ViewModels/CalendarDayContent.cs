using Timetracker.Plugins.Calendar.Services;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// The immutable state of one day column after a calendar build: its clipped
/// session blocks, activity strip spans, contributor text, untracked gaps and
/// distribution plan.
/// </summary>
public sealed record CalendarDayContent(
    DateTimeOffset Date,
    IReadOnlyList<CalendarSessionViewModel> Sessions,
    IReadOnlyList<TimeRange> ActiveSpans,
    string ContributorText,
    TimeSpan ActiveTime,
    IReadOnlyList<TimeRange> Gaps,
    DistributionPlan Distribution);
