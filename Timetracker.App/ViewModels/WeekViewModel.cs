using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Timetracker.App.Models;
using Timetracker.App.Services;
using Timetracker.App.ViewModels.Mvvm;

namespace Timetracker.App.ViewModels;

/// <summary>
/// Week view state: seven day nodes (Monday first) for the selected week, each a
/// collapsible tree of booking elements and their tasks, with navigation
/// backwards/forwards. Starts on the current week. Changing the week collapses
/// every node; a live data refresh keeps the user's expansion.
/// </summary>
public sealed class WeekViewModel : ObservableObject
{
    private readonly ObservableCollection<WeekDayViewModel> _days = new();
    private readonly RelayCommand _previousWeekCommand;
    private readonly RelayCommand _nextWeekCommand;
    private readonly RelayCommand _currentWeekCommand;

    private IReadOnlyList<TrackerEntry> _sessions = [];
    private DateTimeOffset? _runningSince;
    private DateTimeOffset _weekStart;
    private string _weekTitle = "";
    private string _weekTotalText = "";
    private string _statusText = "";
    private WeekStatus _status = WeekStatus.Info;

    public WeekViewModel()
    {
        for (var i = 0; i < 7; i++)
        {
            _days.Add(new WeekDayViewModel());
        }

        _previousWeekCommand = new RelayCommand(() => Move(-7));
        _nextWeekCommand = new RelayCommand(() => Move(7));
        _currentWeekCommand = new RelayCommand(MoveToCurrentWeek);

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
    /// Supplies the per-day contributor line (e.g. PC activity) shown on each day
    /// node. Wired by the composition root; kept as a delegate so the view model
    /// stays free of the plugin contract.
    /// </summary>
    public Func<DateOnly, string>? DayContributorText { get; set; }

    /// <summary>
    /// Supplies how long the computer was actively used on a day (e.g. from the PC
    /// activity monitor); each day node compares it with its booked time to show the
    /// untracked time. Wired by the composition root like <see cref="DayContributorText"/>.
    /// </summary>
    public Func<DateOnly, TimeSpan>? DayActiveTime { get; set; }

    /// <summary>
    /// Supplies a day's active stretches (e.g. from the PC activity monitor), from
    /// which each day lists its untracked gaps. Wired by the composition root like
    /// <see cref="DayActiveTime"/>, so the view model stays free of the plugin contract.
    /// </summary>
    public Func<DateOnly, IReadOnlyList<TimeRange>>? DayActiveSpans { get; set; }

    /// <summary>
    /// Start of the running timer, or null when none runs. Its time is never listed
    /// as a gap, since it will be saved as a session on Stop.
    /// </summary>
    public DateTimeOffset? RunningSince
    {
        get => _runningSince;
        set
        {
            if (SetProperty(ref _runningSince, value))
            {
                Rebuild();
            }
        }
    }

    /// <summary>Every saved session, e.g. for the task-name suggestions when booking a gap.</summary>
    public IReadOnlyList<TrackerEntry> Sessions => _sessions;

    /// <summary>
    /// Books a gap as a new session (range, task name, booking element) and returns
    /// whether it was saved. Wired by <see cref="TrackerViewModel"/>, which owns the
    /// session log.
    /// </summary>
    public Func<TimeRange, string, string, Task<bool>>? BookGap { get; set; }

    /// <summary>
    /// Dialog state for booking <paramref name="gap"/>, with the gap's times and the
    /// suggestions from every saved session. Lets the view open the dialog without
    /// handling model types itself.
    /// </summary>
    public BookGapViewModel CreateBooking(WeekGapViewModel gap)
    {
        var result = new BookGapViewModel(gap.Range, _sessions);
        return result;
    }

    /// <summary>Books the confirmed dialog state as a session; false when nothing was saved.</summary>
    public async Task<bool> BookAsync(BookGapViewModel booking)
    {
        if (BookGap is null)
        {
            return false;
        }

        var result = await BookGap(booking.Range, booking.TaskName, booking.BookingElement);
        return result;
    }

    /// <summary>
    /// Persists planned session changes (e.g. a distributed gap) and returns whether
    /// they were saved. Wired by <see cref="TrackerViewModel"/>, which owns the log.
    /// </summary>
    public Func<IReadOnlyList<SessionChange>, Task<bool>>? ApplyChanges { get; set; }

    /// <summary>
    /// Distributes <paramref name="gap"/> over its bordering sessions after the user
    /// confirmed the listed changes; false when there is nothing to distribute, the
    /// user declined or the save failed.
    /// </summary>
    /// <param name="confirm">Shows the change lines and returns the user's answer.</param>
    public async Task<bool> DistributeAsync(WeekGapViewModel gap, Func<IReadOnlyList<string>, Task<bool>> confirm)
    {
        if (!gap.CanDistribute || ApplyChanges is null || !await confirm(gap.DistributionLines))
        {
            return false;
        }

        var saved = await ApplyChanges(gap.Distribution);
        if (saved)
        {
            ShowStatus($"✓ Distributed {gap.DurationText} over the neighboring sessions.", WeekStatus.Success);
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
        var skipped = day.RoundingSkippedLines.Select(s => "Skipped: " + s).ToList();
        if (plan.Changes.Count == 0)
        {
            if (skipped.Count > 0)
            {
                ShowStatus("Nothing could be rounded. " + string.Join(" · ", skipped), WeekStatus.Info);
            }
            return false;
        }

        if (ApplyChanges is null || !await confirm([.. day.RoundingLines, .. skipped]))
        {
            return false;
        }

        var saved = await ApplyChanges(plan.Changes);
        if (saved)
        {
            var note = skipped.Count > 0 ? " " + string.Join(" · ", skipped) : "";
            ShowStatus($"✓ Rounded {plan.Tasks.Count} task(s) on {day.Header} to half hours.{note}", WeekStatus.Success);
        }

        return saved;
    }

    public ICommand PreviousWeekCommand => _previousWeekCommand;

    public ICommand NextWeekCommand => _nextWeekCommand;

    public ICommand CurrentWeekCommand => _currentWeekCommand;

    /// <summary>
    /// Refreshes the view against a new session log. The selected week and the
    /// user's expansion are kept, so a refresh while tracking never resets the view.
    /// </summary>
    public void UpdateSessions(IReadOnlyList<TrackerEntry> sessions)
    {
        _sessions = sessions;
        Rebuild();
    }

    private void Move(int days)
    {
        _weekStart = _weekStart.AddDays(days);
        Rebuild();
        CollapseAll();
    }

    private void MoveToCurrentWeek()
    {
        _weekStart = StartOfWeek(DateTimeOffset.Now);
        Rebuild();
        CollapseAll();
    }

    private void Rebuild()
    {
        var weekEnd = _weekStart.AddDays(7);
        var weekSessions = _sessions
            .Where(s => s.Start.Date >= _weekStart.Date && s.Start.Date < weekEnd.Date)
            .ToList();

        // The running timer extends into the future until it is stopped.
        TimeRange? running = _runningSince is { } since ? new TimeRange(since, DateTimeOffset.MaxValue) : null;

        for (var i = 0; i < 7; i++)
        {
            var day = _weekStart.AddDays(i);
            var daySessions = weekSessions.Where(s => s.Start.Date == day.Date).ToList();
            _days[i].Update(day, daySessions);
            _days[i].SetRounding(HalfHourRounding.Plan(daySessions, _sessions, running));
            var date = DateOnly.FromDateTime(day.Date);
            _days[i].ContributorText = DayContributorText?.Invoke(date) ?? "";
            _days[i].ActiveTime = DayActiveTime?.Invoke(date) ?? TimeSpan.Zero;

            // Every session counts, not only the day's: one started the evening before
            // can cover the early hours of this day.
            _days[i].SetGaps(
                UntrackedGaps.Find(
                    DayActiveSpans?.Invoke(date) ?? [], _sessions, running, WeekDayViewModel.MinimumGapDuration),
                _sessions);
        }

        WeekTotalText = $"Σ {WeekTimeFormat.HoursMinutes(weekSessions.Sum(s => s.DurationSeconds))}";
        WeekTitle = $"Week {ISOWeek.GetWeekOfYear(_weekStart.LocalDateTime):00} · {FormatRange(_weekStart)}";
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
