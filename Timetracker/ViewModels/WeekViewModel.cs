using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Timetracker.Models;

namespace Timetracker.ViewModels;

/// <summary>
/// Week view state: seven weekday columns (Monday first) for the selected week,
/// with navigation backwards/forwards. Starts on the current week.
/// </summary>
public sealed class WeekViewModel : ObservableObject
{
    private readonly BindingList<WeekDayViewModel> _days = new();
    private readonly RelayCommand _previousWeekCommand;
    private readonly RelayCommand _nextWeekCommand;
    private readonly RelayCommand _currentWeekCommand;

    private IReadOnlyList<TrackerEntry> _sessions = [];
    private DateTimeOffset _weekStart;
    private string _weekTitle = "";
    private string _weekTotalText = "";
    private bool _groupByBookingElement = true;

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
    public BindingList<WeekDayViewModel> Days => _days;

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

    /// <summary>
    /// True: day columns group entries by booking element (default). False: they
    /// show the task names instead.
    /// </summary>
    public bool GroupByBookingElement
    {
        get => _groupByBookingElement;
        set
        {
            if (SetProperty(ref _groupByBookingElement, value))
            {
                Rebuild();
            }
        }
    }

    public ICommand PreviousWeekCommand => _previousWeekCommand;

    public ICommand NextWeekCommand => _nextWeekCommand;

    public ICommand CurrentWeekCommand => _currentWeekCommand;

    /// <summary>
    /// Refreshes the view against a new session log. The selected week is kept,
    /// so a refresh never jumps the user back to the current week.
    /// </summary>
    public void UpdateSessions(IReadOnlyList<TrackerEntry> sessions)
    {
        _sessions = sessions;
        Rebuild();
    }

    /// <summary>Sets the grouping mode without changing the grouping itself (for binding only).</summary>
    public void SetGrouping(bool groupByBookingElement)
    {
        _groupByBookingElement = groupByBookingElement;
        OnPropertyChanged(nameof(GroupByBookingElement));
        Rebuild();
    }

    private void Move(int days)
    {
        _weekStart = _weekStart.AddDays(days);
        Rebuild();
    }

    private void MoveToCurrentWeek()
    {
        _weekStart = StartOfWeek(DateTimeOffset.Now);
        Rebuild();
    }

    private void Rebuild()
    {
        var weekEnd = _weekStart.AddDays(7);
        var weekSessions = _sessions
            .Where(s => s.Start.Date >= _weekStart.Date && s.Start.Date < weekEnd.Date)
            .ToList();

        for (var i = 0; i < 7; i++)
        {
            var day = _weekStart.AddDays(i);
            _days[i].Update(day, weekSessions.Where(s => s.Start.Date == day.Date), GroupByBookingElement);
        }

        WeekTotalText = $"Σ {HoursMinutes(weekSessions.Sum(s => s.DurationSeconds))}";
        WeekTitle = $"Week {ISOWeek.GetWeekOfYear(_weekStart.LocalDateTime):00} · {FormatRange(_weekStart)}";
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

    private static string HoursMinutes(double seconds) =>
        TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"h\:mm");
}
