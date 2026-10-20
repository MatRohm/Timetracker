using Timetracker.Plugins.Calendar.Services;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Builds a <see cref="CalendarContent"/> from the session log and the add-in
/// contributors: clips sessions to days, collects activity spans and gaps, fits the
/// time axis, places the now line and fills the month grid. Pure data assembly, so
/// the view model only publishes the result.
/// </summary>
internal sealed class CalendarBuilder(
    IReadOnlyList<ICalendarDayQuery> dayContributors,
    IReadOnlyList<IDayActivityQuery> activitySources,
    Func<DateTimeOffset> now)
{
    public CalendarContent Build(
        DateTimeOffset weekStart,
        DateTimeOffset monthStart,
        CalendarViewMode mode,
        IReadOnlyList<TrackedSession> sessions,
        DateTimeOffset? runningSince)
    {
        var moment = now();
        var days = new List<CalendarDayContent>(7);

        for (var i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            var dayStart = new DateTimeOffset(day.Date, day.Offset);
            var dayEnd = dayStart.AddDays(1);

            var blocks = new List<CalendarSessionViewModel>();
            foreach (var session in sessions)
            {
                var end = session.End > session.Start ? session.End : moment;
                if (session.Start < dayEnd && end > dayStart)
                {
                    var isRunning = runningSince is { } since && since == session.Start;
                    blocks.Add(CalendarSessionViewModel.FromSession(session, moment, dayStart, dayEnd, isRunning));
                }
            }

            var date = DateOnly.FromDateTime(day.Date);
            var activeTime = activitySources
                .Select(s => s.GetActiveTime(date))
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
            var spans = activitySources
                .SelectMany(s => s.GetActiveSpans(date))
                .Select(span => new TimeRange(
                    span.Start > dayStart ? span.Start : dayStart,
                    span.End < dayEnd ? span.End : dayEnd))
                .Where(range => range.Duration > TimeSpan.Zero)
                .ToList();

            var booked = blocks.Sum(b => b.Duration.TotalSeconds);
            var untracked = activeTime - TimeSpan.FromSeconds(booked);
            if (untracked < TimeSpan.Zero)
            {
                untracked = TimeSpan.Zero;
            }

            var daySessions = sessions.Where(s => s.Start.Date == day.Date).ToList();
            var gaps = UntrackedGaps.Find(
                activitySources.SelectMany(s => s.GetActiveSpans(date))
                    .Select(span => new TimeRange(span.Start, span.End))
                    .ToList(),
                sessions,
                runningSince is { } runningStart ? new TimeRange(runningStart, DateTimeOffset.MaxValue) : null,
                CalendarDayViewModel.MinimumGapDuration);

            days.Add(new CalendarDayContent(
                day,
                blocks,
                spans,
                string.Join(" · ", dayContributors.Select(c => c.GetDayText(date)).Where(text => text.Length > 0)),
                activeTime,
                gaps,
                DayDistribution.Plan(daySessions, untracked)));
        }

        var visible = mode == CalendarViewMode.Workweek ? days.Take(5).ToList() : days;
        var ranges = visible
            .SelectMany(d => d.Sessions)
            .Select(s => new TimeRange(s.Start, s.End))
            .ToList();
        var visibleToday = visible.Any(d => d.Date.Date == moment.Date);
        var axis = CalendarLayout.FitAxis(ranges, visibleToday ? moment.TimeOfDay : null);
        var nowLine = visibleToday && axis.Contains(moment.TimeOfDay)
            ? (moment.TimeOfDay - axis.Start).TotalMinutes
            : (double?)null;

        var monthDays = MonthGridBuilder.Build(monthStart, sessions, moment);
        var visibleSeconds = mode == CalendarViewMode.Month
            ? monthDays.Where(d => d.IsInMonth).Sum(d => d.TotalSeconds)
            : visible.Sum(d => d.Sessions.Sum(s => s.Duration.TotalSeconds));

        var result = new CalendarContent(days, axis, nowLine, monthDays, visibleSeconds);
        return result;
    }
}
