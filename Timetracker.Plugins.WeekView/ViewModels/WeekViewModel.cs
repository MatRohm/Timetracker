using System.Collections.ObjectModel;
using Timetracker.Plugins.WeekView.Localization;
using System.Globalization;
using System.Windows.Input;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;
using Timetracker.Plugins.WeekView.Services;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// Week view state: seven day nodes (Monday first) for the selected week, each a
/// collapsible tree of booking elements and their tasks, with navigation
/// backwards/forwards. Starts on the current week. Changing the week collapses
/// every node; a live data refresh keeps the user's expansion.
/// </summary>
public sealed class WeekViewModel : ObservableObject
{
    private readonly ITrackedSessionsQuery _sessions;
    private readonly ITrackedSessionsCommand _sessionCommands;
    private readonly IReadOnlyList<IWeekDayQuery> _dayContributors;
    private readonly IReadOnlyList<IDayActivityQuery> _activitySources;

    /// <summary>Current time; replaceable so the current-week highlight can be tested deterministically.</summary>
    private readonly Func<DateTimeOffset> _now;

    private readonly ObservableCollection<WeekDayViewModel> _days = new();
    private readonly RelayCommand _previousWeekCommand;
    private readonly RelayCommand _nextWeekCommand;
    private readonly RelayCommand _currentWeekCommand;

    private DateTimeOffset _weekStart;
    private string _weekTitle = "";
    private string _weekTotalText = "";
    private string _statusText = "";
    private WeekStatus _status = WeekStatus.Info;

    public WeekViewModel(
        ITrackedSessionsQuery sessions,
        ITrackedSessionsCommand sessionCommands,
        IEnumerable<IWeekDayQuery> dayContributors,
        IEnumerable<IDayActivityQuery> activitySources,
        Func<DateTimeOffset>? now = null)
    {
        _sessions = sessions;
        _sessionCommands = sessionCommands;
        _dayContributors = dayContributors.ToList();
        _activitySources = activitySources.ToList();
        _now = now ?? (() => DateTimeOffset.Now);

        for (var i = 0; i < 7; i++)
        {
            _days.Add(new WeekDayViewModel(_now));
        }

        _previousWeekCommand = new RelayCommand(() => Move(-7));
        _nextWeekCommand = new RelayCommand(() => Move(7));
        _currentWeekCommand = new RelayCommand(MoveToCurrentWeek);

        // The app owns the session log; a save, start or stop refreshes the view.
        _sessions.Changed += OnSessionsChanged;

        MoveToCurrentWeek();
    }

    /// <summary>Exactly seven items, Monday first; instances are stable across rebuilds.</summary>
    public ObservableCollection<WeekDayViewModel> Days => _days;

    public string WeekTitle
    {
        get => _weekTitle;
        private set => SetProperty(ref _weekTitle, value);
    }

    public string WeekTotalText
    {
        get => _weekTotalText;
        private set => SetProperty(ref _weekTotalText, value);
    }

    /// <summary>Text of the shared status line, shown at the bottom of the view.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Kind of the current status message; the view maps it to a color.</summary>
    public WeekStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Shows a status message (from the app or an add-in) in the shared status line.</summary>
    public void ShowStatus(string message, WeekStatus kind)
    {
        Status = kind;
        StatusText = message;
    }

    /// <summary>
    /// Dialog state for booking <paramref name="gap"/>, with the gap's times and the
    /// suggestions from every saved session. Lets the view open the dialog without
    /// handling model types itself.
    /// </summary>
    public BookGapViewModel CreateBooking(WeekGapViewModel gap)
    {
        var result = new BookGapViewModel(gap.Range, _sessions.Sessions);
        return result;
    }

    /// <summary>Books the confirmed dialog state as a session; false when nothing was saved.</summary>
    public async Task<bool> BookAsync(BookGapViewModel booking)
    {
        var result = await _sessionCommands.BookAsync(booking.Range, booking.TaskName, booking.BookingElement);
        return result;
    }

    /// <summary>
    /// Dialog state for distributing <paramref name="day"/>'s untracked time over its
    /// tasks: one row per task with a proportional prefilled share and −/+ steppers
    /// in 15-minute steps. Lets the view open the dialog without handling model types.
    /// </summary>
    public DistributeDayViewModel CreateDistributeDay(WeekDayViewModel day)
    {
        var daySessions = _sessions.Sessions.Where(s => s.Start.Date == day.Date.Date).ToList();
        TimeRange? running = _sessions.RunningSince is { } since ? new TimeRange(since, DateTimeOffset.MaxValue) : null;
        var result = new DistributeDayViewModel(day.Header, day.UntrackedTime, daySessions, _sessions.Sessions, running);
        return result;
    }

    /// <summary>Applies the confirmed distribute plan; false when nothing was applied.</summary>
    public async Task<bool> ApplyDistributeAsync(WeekDayViewModel day, DistributionPlan plan)
    {
        if (plan.Changes.Count == 0)
        {
            ShowStatus(Strings.Week_NothingToDistribute, WeekStatus.Info);
            return false;
        }

        var saved = await _sessionCommands.ApplyChangesAsync(plan.Changes);
        if (saved)
        {
            var assigned = TimeSpan.Zero;
            foreach (var task in plan.Tasks)
            {
                assigned += task.Share;
            }
            ShowStatus(string.Format(Strings.Week_Distributed, WeekTimeFormat.HoursMinutes(assigned), plan.Tasks.Count, day.Header), WeekStatus.Success);
        }

        return saved;
    }

    /// <summary>
    /// Rounds each task's total on <paramref name="day"/> to the nearest half hour
    /// after the user confirmed the listed changes (skipped tasks are listed too).
    /// False when nothing could be rounded, the user declined or the save failed.
    /// </summary>
    /// <param name="confirm">Shows the change lines and returns the user's answer.</param>
    public async Task<bool> RoundDayAsync(WeekDayViewModel day, Func<IReadOnlyList<string>, Task<bool>> confirm)
    {
        var plan = day.Rounding;
        var skipped = day.RoundingSkippedLines.Select(s => Strings.Week_Skipped + s).ToList();
        if (plan.Changes.Count == 0)
        {
            if (skipped.Count > 0)
            {
                ShowStatus(Strings.Week_NothingToRound + string.Join(" · ", skipped), WeekStatus.Info);
            }
            return false;
        }

        if (!await confirm([.. day.RoundingLines, .. skipped]))
        {
            return false;
        }

        var saved = await _sessionCommands.ApplyChangesAsync(plan.Changes);
        if (saved)
        {
            var note = skipped.Count > 0 ? " " + string.Join(" · ", skipped) : "";
            ShowStatus(string.Format(Strings.Week_Rounded, plan.Tasks.Count, day.Header, note), WeekStatus.Success);
        }

        return saved;
    }

    public ICommand PreviousWeekCommand => _previousWeekCommand;

    public ICommand NextWeekCommand => _nextWeekCommand;

    public ICommand CurrentWeekCommand => _currentWeekCommand;

    private void OnSessionsChanged(object? sender, EventArgs e) => Rebuild();

    private void Move(int days)
    {
        _weekStart = _weekStart.AddDays(days);
        Rebuild();
        CollapseAll();
    }

    private void MoveToCurrentWeek()
    {
        _weekStart = StartOfWeek(_now());
        Rebuild();
        CollapseAll();
    }

    private void Rebuild()
    {
        var sessions = _sessions.Sessions;
        var weekEnd = _weekStart.AddDays(7);
        var weekSessions = sessions
            .Where(s => s.Start.Date >= _weekStart.Date && s.Start.Date < weekEnd.Date)
            .ToList();

        // The running timer extends into the future until it is stopped.
        TimeRange? running = _sessions.RunningSince is { } since ? new TimeRange(since, DateTimeOffset.MaxValue) : null;

        for (var i = 0; i < 7; i++)
        {
            var day = _weekStart.AddDays(i);
            var daySessions = weekSessions.Where(s => s.Start.Date == day.Date).ToList();
            _days[i].Update(day, daySessions);
            _days[i].SetRounding(HalfHourRounding.Plan(daySessions, sessions, running));
            var date = DateOnly.FromDateTime(day.Date);
            _days[i].ContributorText = string.Join(" · ",
                _dayContributors.Select(c => c.GetDayText(date)).Where(text => text.Length > 0));
            _days[i].ActiveTime = _activitySources
                .Select(s => s.GetActiveTime(date))
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
            _days[i].SetDistribution(DayDistribution.Plan(daySessions, _days[i].UntrackedTime, sessions, running));

            // Every session counts, not only the day's: one started the evening before
            // can cover the early hours of this day.
            _days[i].SetGaps(
                UntrackedGaps.Find(
                    [.. _activitySources
                        .SelectMany(s => s.GetActiveSpans(date))
                        .Select(span => new TimeRange(span.Start, span.End))],
                    sessions, running, WeekDayViewModel.MinimumGapDuration));
        }

        WeekTotalText = string.Format(Strings.Week_TotalText, WeekTimeFormat.HoursMinutes(weekSessions.Sum(s => s.DurationSeconds)));
        WeekTitle = string.Format(Strings.Week_HeaderTitle, ISOWeek.GetWeekOfYear(_weekStart.LocalDateTime).ToString("00"), FormatRange(_weekStart));
    }

    /// <summary>Collapses every day (and group) of the selected week.</summary>
    private void CollapseAll()
    {
        foreach (var day in _days)
        {
            day.CollapseAll();
        }
    }

    /// <summary>Monday 00:00 of the week containing <paramref name="moment"/>.</summary>
    private static DateTimeOffset StartOfWeek(DateTimeOffset moment)
    {
        var back = ((int)moment.Date.DayOfWeek + 6) % 7; // Sunday=0 → 6 days back
        return new DateTimeOffset(moment.Date.AddDays(-back), moment.Offset);
    }

    private static string FormatRange(DateTimeOffset start)
    {
        var end = start.AddDays(6);
        var startText = start.ToString("MMM d", CultureInfo.InvariantCulture);

        // Keep the month on the end date when the range crosses a month boundary.
        var endText = start.Month == end.Month
            ? $"{end.Day}, {end:yyyy}"
            : $"{end:MMM d}, {end:yyyy}";

        if (start.Year != end.Year)
        {
            return $"{start:MMM d, yyyy} – {endText}, {end:yyyy}";
        }

        return $"{startText} – {endText}";
    }
}
