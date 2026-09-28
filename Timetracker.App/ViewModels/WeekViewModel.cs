using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Timetracker.App.Models;

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

        for (var i = 0; i < 7; i++)
        {
            var day = _weekStart.AddDays(i);
            _days[i].Update(day, weekSessions.Where(s => s.Start.Date == day.Date));
            _days[i].ContributorText =
                DayContributorText?.Invoke(DateOnly.FromDateTime(day.Date)) ?? "";
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
