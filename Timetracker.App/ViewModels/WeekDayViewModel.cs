using System.Globalization;
using Timetracker.ActivityMonitor;
using Timetracker.Models;

namespace Timetracker.ViewModels;

/// <summary>One weekday column in the week view; the instance is updated in place on rebuild.</summary>
public sealed class WeekDayViewModel : ObservableObject
{
    private string _header = "";
    private string _entriesText = "";
    private string _totalText = "";
    private bool _isToday;

    public DateTimeOffset Date { get; private set; }

    /// <summary>Column caption, e.g. "Mon 14.09.".</summary>
    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    /// <summary>All bookings of the day, one line per session.</summary>
    public string EntriesText
    {
        get => _entriesText;
        private set => SetProperty(ref _entriesText, value);
    }

    /// <summary>Sum of the day's durations, e.g. "Σ 2:30".</summary>
    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    /// <summary>True when this column is today; the view highlights it.</summary>
    public bool IsToday
    {
        get => _isToday;
        private set => SetProperty(ref _isToday, value);
    }

    /// <summary>
    /// The day's lines: one per booking element while grouping by element, else one
    /// per task. Each carries the text its copy button copies. Empty for a day with
    /// no bookings.
    /// </summary>
    public IReadOnlyList<WeekDayGroup> Groups { get; private set; } = [];

    public void Update(DateTimeOffset date, IEnumerable<TrackerEntry> sessions, bool groupByBookingElement)
    {
        var items = sessions.OrderBy(e => e.Start).ToList();

        Date = date;
        Header = $"{date.ToString("ddd", CultureInfo.InvariantCulture)} {date.Day:00}.{date.Month:00}.";

        // One line per group: entries are merged and only the total duration is
        // shown (case-insensitive, like everywhere else). The group key is either
        // the booking element (default) or the task name.
        var grouped = items
            .GroupBy(
                e => groupByBookingElement ? e.BookingElement.Trim() : e.Task.Trim(),
                StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Min(e => e.Start))
            .ToList();

        Groups = [.. grouped.Select(g => new WeekDayGroup(
            $"{GroupLabel(g, groupByBookingElement)} ({HoursMinutes(g.Sum(e => e.DurationSeconds))})",
            GroupLabel(g, groupByBookingElement)))];

        EntriesText = string.Join(Environment.NewLine, Groups.Select(g => g.Label));

        TotalText = items.Count > 0 ? $"Σ {HoursMinutes(items.Sum(e => e.DurationSeconds))}" : "";
        IsToday = date.Date == DateTimeOffset.Now.Date;
        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(Groups));
    }

    /// <summary>
    /// Sets the PC activity line for this day: the sum of active and idle spans
    /// overlapping this date, e.g. "PC 7:15 active". Empty when no data exists.
    /// </summary>
    public void UpdateActivity(IEnumerable<ActivitySpan> spans)
    {
        var daySpans = spans
            .Select(s => (Span: s, From: s.Start, To: s.End))
            .ToList();

        var activeSeconds = 0.0;
        var idleSeconds = 0.0;
        foreach (var (span, from, to) in daySpans)
        {
            // Clamps a span to this day; spans crossing midnight count on both
            // days with their respective parts.
            var dayStart = Date.Date;
            var dayEnd = dayStart.AddDays(1);
            var overlapStart = from > dayStart ? from : dayStart;
            var overlapEnd = to < dayEnd ? to : dayEnd;
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var seconds = (overlapEnd - overlapStart).TotalSeconds;
            if (span.Kind == "active")
            {
                activeSeconds += seconds;
            }
            else
            {
                idleSeconds += seconds;
            }
        }

        if (activeSeconds <= 0 && idleSeconds <= 0)
        {
            ActivityText = "";
            return;
        }

        var result = "PC " + HoursMinutes(activeSeconds) + " active";
        if (idleSeconds > 0)
        {
            result += " · " + HoursMinutes(idleSeconds) + " idle";
        }
        ActivityText = result;
    }

    /// <summary>PC activity line for this day, e.g. "PC 7:15 active · 1:20 idle".</summary>
    public string ActivityText
    {
        get => _activityText;
        private set => SetProperty(ref _activityText, value);
    }

    private string _activityText = "";

    /// <summary>Falls back to "(without)" when the group key is empty (no booking element set).</summary>
    private static string GroupLabel(IGrouping<string, TrackerEntry> group, bool groupByBookingElement)
    {
        var key = group.Key;
        if (key.Length > 0)
        {
            return key;
        }

        if (!groupByBookingElement)
        {
            // Grouping by task: the key is already the meaningful name.
            return "(without)";
        }

        // Grouping by booking element: show the task names contained in the group.
        return string.Join(", ", group
            .Select(e => e.Task.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase));
    }

    private static string HoursMinutes(double seconds) =>
        TimeSpan.FromSeconds(Math.Round(seconds)).ToString(@"h\:mm");
}
