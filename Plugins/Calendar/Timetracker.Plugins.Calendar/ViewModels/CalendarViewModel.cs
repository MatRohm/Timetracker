using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Timetracker.Plugins.Calendar.Localization;
using Timetracker.Plugins.Calendar.Services;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Calendar state: Workweek/Week show seven day columns (Monday first) on a fitted
/// time axis, Month shows a 6x7 grid. Navigation moves by week or month depending
/// on the mode, and the selected mode is remembered in the option store.
/// </summary>
public sealed class CalendarViewModel : ObservableObject
{
    private readonly ITrackedSessionsQuery _sessions;
    private readonly ITrackedSessionsCommand _sessionCommands;
    private readonly IOptionQuery _options;
    private readonly IOptionCommand _optionCommands;
    private readonly CalendarBuilder _builder;

    /// <summary>Current time; replaceable so the now line and today's column are testable.</summary>
    private readonly Func<DateTimeOffset> _now;

    private readonly ObservableCollection<CalendarDayViewModel> _days = new();
    private readonly RelayCommand _previousCommand;
    private readonly RelayCommand _nextCommand;
    private readonly RelayCommand _currentCommand;
    private readonly AsyncRelayCommand _workweekCommand;
    private readonly AsyncRelayCommand _weekCommand;
    private readonly AsyncRelayCommand _monthCommand;

    private CalendarViewMode _mode;
    private DateTimeOffset _weekStart;
    private DateTimeOffset _monthStart;
    private CalendarAxis _axis = CalendarAxis.Default;
    private double? _nowLineMinutes;
    private IReadOnlyList<MonthDayViewModel> _monthDays = [];
    private string _calendarTitle = "";
    private string _calendarTotalText = "";
    private string _statusText = "";
    private CalendarStatusKind _status = CalendarStatusKind.Info;

    public CalendarViewModel(
        ITrackedSessionsQuery sessions,
        ITrackedSessionsCommand sessionCommands,
        IEnumerable<ICalendarDayQuery> dayContributors,
        IEnumerable<IDayActivityQuery> activitySources,
        IOptionQuery options,
        IOptionCommand optionCommands,
        Func<DateTimeOffset>? now = null)
    {
        _sessions = sessions;
        _sessionCommands = sessionCommands;
        _options = options;
        _optionCommands = optionCommands;
        _now = now ?? (() => DateTimeOffset.Now);
        _builder = new CalendarBuilder(dayContributors.ToList(), activitySources.ToList(), _now);
        _mode = ReadMode(_options);

        for (var i = 0; i < 7; i++)
        {
            _days.Add(new CalendarDayViewModel(_now));
        }

        _previousCommand = new RelayCommand(() => Move(-1));
        _nextCommand = new RelayCommand(() => Move(1));
        _currentCommand = new RelayCommand(MoveToCurrent);
        _workweekCommand = new AsyncRelayCommand(() => ChangeModeAsync(CalendarViewMode.Workweek));
        _weekCommand = new AsyncRelayCommand(() => ChangeModeAsync(CalendarViewMode.Week));
        _monthCommand = new AsyncRelayCommand(() => ChangeModeAsync(CalendarViewMode.Month));

        // The app owns the session log; a save, start or stop refreshes the view.
        _sessions.Changed += OnSessionsChanged;

        MoveToCurrent();
    }

    /// <summary>Exactly seven items, Monday first; instances are stable across rebuilds.</summary>
    public ObservableCollection<CalendarDayViewModel> Days => _days;

    /// <summary>The day columns the current mode shows: five for Workweek, seven for Week.</summary>
    public IReadOnlyList<CalendarDayViewModel> VisibleDays =>
        [.. _days.Take(Mode == CalendarViewMode.Workweek ? 5 : 7)];

    /// <summary>Current layout.</summary>
    public CalendarViewMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(VisibleDays));
            }
        }
    }

    /// <summary>Fitted time window of the Workweek/Week grid.</summary>
    public CalendarAxis Axis
    {
        get => _axis;
        private set => SetProperty(ref _axis, value);
    }

    /// <summary>
    /// Position of the red now line in minutes from <see cref="Axis"/>'s start;
    /// null when today is not a visible column or now is outside the axis.
    /// </summary>
    public double? NowLineMinutes
    {
        get => _nowLineMinutes;
        private set => SetProperty(ref _nowLineMinutes, value);
    }

    /// <summary>The 42 cells of the month grid; empty until Month mode is built.</summary>
    public IReadOnlyList<MonthDayViewModel> MonthDays
    {
        get => _monthDays;
        private set => SetProperty(ref _monthDays, value);
    }

    public string CalendarTitle
    {
        get => _calendarTitle;
        private set => SetProperty(ref _calendarTitle, value);
    }

    public string CalendarTotalText
    {
        get => _calendarTotalText;
        private set => SetProperty(ref _calendarTotalText, value);
    }

    /// <summary>Text of the shared status line, shown at the bottom of the view.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Kind of the current status message; the view maps it to a color.</summary>
    public CalendarStatusKind Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Shows a status message (from the app or an add-in) in the shared status line.</summary>
    public void ShowStatus(string message, CalendarStatusKind kind)
    {
        Status = kind;
        StatusText = message;
    }

    /// <summary>
    /// Dialog state for booking <paramref name="gap"/>, with the gap's times and the
    /// suggestions from every saved session. Lets the view open the dialog without
    /// handling model types itself.
    /// </summary>
    public BookGapViewModel CreateBooking(CalendarGapViewModel gap)
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
    public DistributeDayViewModel CreateDistributeDay(CalendarDayViewModel day)
    {
        var daySessions = _sessions.Sessions.Where(s => s.Start.Date == day.Date.Date).ToList();
        var result = new DistributeDayViewModel(day.Header, day.UntrackedTime, daySessions);
        return result;
    }

    /// <summary>Applies the confirmed distribute plan; false when nothing was applied.</summary>
    public async Task<bool> ApplyDistributeAsync(CalendarDayViewModel day, DistributionPlan plan)
    {
        if (plan.Changes.Count == 0)
        {
            ShowStatus(Strings.Calendar_NothingToDistribute, CalendarStatusKind.Info);
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
            ShowStatus(string.Format(Strings.Calendar_Distributed, CalendarTimeFormat.HoursMinutes(assigned), plan.Tasks.Count, day.Header), CalendarStatusKind.Success);
        }

        return saved;
    }

    /// <summary>Shows the week containing <paramref name="date"/>, switching to Week mode.</summary>
    public async Task ShowWeekAsync(DateTimeOffset date) =>
        await ChangeModeAsync(CalendarViewMode.Week, date);

    /// <summary>
    /// Rebuilds the view for the current clock. Called once a minute by the view so
    /// the now line and a running block stay current.
    /// </summary>
    public void RefreshNow() => Rebuild();

    public ICommand PreviousCommand => _previousCommand;

    public ICommand NextCommand => _nextCommand;

    public ICommand CurrentCommand => _currentCommand;

    public ICommand WorkweekCommand => _workweekCommand;

    public ICommand WeekCommand => _weekCommand;

    public ICommand MonthCommand => _monthCommand;

    private void OnSessionsChanged(object? sender, EventArgs e) => Rebuild();

    private void Move(int step)
    {
        if (Mode == CalendarViewMode.Month)
        {
            _monthStart = _monthStart.AddMonths(step);
        }
        else
        {
            _weekStart = _weekStart.AddDays(step * 7);
        }

        Rebuild();
    }

    private void MoveToCurrent()
    {
        var now = _now();
        _weekStart = CalendarDates.StartOfWeek(now);
        _monthStart = CalendarDates.FirstOfMonth(now);
        Rebuild();
    }

    /// <summary>
    /// Switches the mode. When <paramref name="weekStart"/> is given (a month cell
    /// was clicked) that week is kept instead of deriving one from the month.
    /// </summary>
    internal async Task ChangeModeAsync(CalendarViewMode mode, DateTimeOffset? weekStart = null)
    {
        var changed = Mode != mode;
        if (!changed && weekStart is null)
        {
            return;
        }

        var previous = Mode;
        Mode = mode;

        if (weekStart is { } target)
        {
            _weekStart = CalendarDates.StartOfWeek(target);
        }
        else if (mode == CalendarViewMode.Month)
        {
            // A week can straddle two months; the Thursday decides which month the
            // user means (a week starting 28 Sep belongs to October).
            _monthStart = CalendarDates.FirstOfMonth(_weekStart.AddDays(3));
        }
        else if (previous == CalendarViewMode.Month
            && (_weekStart.Year != _monthStart.Year || _weekStart.Month != _monthStart.Month))
        {
            _weekStart = CalendarDates.StartOfWeek(_monthStart);
        }

        Rebuild();

        if (!changed)
        {
            return;
        }

        try
        {
            await _optionCommands.SetValueAsync(CalendarOptions.ViewModeKey, mode.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(Strings.Calendar_ModeSaveFailed, CalendarStatusKind.Error);
        }
    }

    private void Rebuild()
    {
        var content = _builder.Build(
            _weekStart,
            _monthStart,
            Mode,
            _sessions.Sessions,
            _sessions.RunningSince);

        for (var i = 0; i < _days.Count; i++)
        {
            _days[i].Apply(content.Days[i]);
        }

        Axis = content.Axis;
        NowLineMinutes = content.NowLineMinutes;
        MonthDays = content.MonthDays;

        CalendarTotalText = content.VisibleSeconds > 0
            ? string.Format(Strings.Calendar_TotalText, CalendarTimeFormat.HoursMinutes(content.VisibleSeconds))
            : "";
        CalendarTitle = Mode == CalendarViewMode.Month
            ? string.Format(
                Strings.Calendar_MonthTitle,
                _monthStart.ToString("MMMM", CultureInfo.InvariantCulture),
                _monthStart.Year)
            : string.Format(
                Mode == CalendarViewMode.Workweek ? Strings.Calendar_WorkweekTitle : Strings.Calendar_HeaderTitle,
                ISOWeek.GetWeekOfYear(_weekStart.LocalDateTime).ToString("00"),
                FormatRange(_weekStart));

        // One signal at the end of a rebuild: the day view models change many
        // properties in a row, and the view only needs to repaint once.
        OnPropertyChanged(nameof(Days));
    }

    private static CalendarViewMode ReadMode(IOptionQuery options)
    {
        var stored = options.GetValue(CalendarOptions.ViewModeKey);
        return Enum.TryParse<CalendarViewMode>(stored, ignoreCase: true, out var mode) && Enum.IsDefined(mode)
            ? mode
            : CalendarViewMode.Workweek;
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
