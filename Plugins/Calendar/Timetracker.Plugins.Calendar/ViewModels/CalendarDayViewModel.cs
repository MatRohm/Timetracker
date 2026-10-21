using System.Globalization;
using Timetracker.Plugins.Calendar.Localization;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;
using Timetracker.Plugins.Calendar.Services;

namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// One day column of the calendar: the weekday caption, the day's summed time,
/// its clipped session blocks, the activity strip spans and the untracked gaps.
/// The instance is updated in place on rebuild, so the view can observe it.
/// </summary>
public sealed class CalendarDayViewModel(Func<DateTimeOffset>? now = null) : ObservableObject
{
    /// <summary>Untracked time from which the day header warns (highlighted, "⚠").</summary>
    public static readonly TimeSpan UntrackedWarningThreshold = TimeSpan.FromMinutes(15);

    /// <summary>Shortest untracked stretch listed as a gap in the day column.</summary>
    public static readonly TimeSpan MinimumGapDuration = TimeSpan.FromMinutes(15);

    /// <summary>Gaps shorter than this are rounding noise and not shown at all.</summary>
    private static readonly TimeSpan MinimumUntrackedTime = TimeSpan.FromMinutes(1);

    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.Now);

    private string _header = "";
    private string _totalText = "";
    private bool _isToday;
    private string _contributorText = "";
    private double _bookedSeconds;
    private TimeSpan _activeTime;
    private TimeSpan _untrackedTime;
    private string _untrackedText = "";
    private bool _isUntrackedWarning;

    public DateTimeOffset Date { get; private set; }

    /// <summary>Column caption, e.g. "Mon 14.09.".</summary>
    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    /// <summary>Sum of the day's clipped durations, e.g. "Σ 2:30".</summary>
    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    /// <summary>True when this day is today; the view highlights it.</summary>
    public bool IsToday
    {
        get => _isToday;
        private set => SetProperty(ref _isToday, value);
    }

    /// <summary>Per-day contributor line (e.g. "PC 7:15 active · 1:20 idle"); empty when none.</summary>
    public string ContributorText
    {
        get => _contributorText;
        set => SetProperty(ref _contributorText, value);
    }

    /// <summary>
    /// How long the computer was actively used on this day (from the PC activity
    /// monitor); zero when nothing was recorded. Drives <see cref="UntrackedText"/>.
    /// </summary>
    public TimeSpan ActiveTime
    {
        get => _activeTime;
        set
        {
            if (SetProperty(ref _activeTime, value))
            {
                RefreshUntracked();
            }
        }
    }

    /// <summary>
    /// Active time not covered by booked sessions, e.g. "⚠ 1:05 untracked"; empty
    /// when there is no activity or the bookings cover it.
    /// </summary>
    public string UntrackedText
    {
        get => _untrackedText;
        private set => SetProperty(ref _untrackedText, value);
    }

    /// <summary>
    /// Active time not covered by booked sessions (zero when the bookings cover
    /// it); the missing time the day's Distribute action hands out.
    /// </summary>
    public TimeSpan UntrackedTime => _untrackedTime;

    /// <summary>True when the untracked time reaches <see cref="UntrackedWarningThreshold"/>.</summary>
    public bool IsUntrackedWarning
    {
        get => _isUntrackedWarning;
        private set => SetProperty(ref _isUntrackedWarning, value);
    }

    /// <summary>
    /// The day's session blocks, clipped to this day and ordered by start; a
    /// session crossing midnight appears in both days.
    /// </summary>
    public IReadOnlyList<CalendarSessionViewModel> Sessions { get; private set; } = [];

    /// <summary>
    /// The day's booking elements for the header's copy flyout, ordered by
    /// earliest session; sessions without an element form a &lt;None&gt; group.
    /// </summary>
    public IReadOnlyList<CalendarCopyGroupViewModel> CopyGroups { get; private set; } = [];

    /// <summary>
    /// Active stretches of this day, clipped to the day and ordered by start;
    /// the view draws one green strip segment per span.
    /// </summary>
    public IReadOnlyList<TimeRange> ActiveSpans { get; private set; } = [];

    /// <summary>
    /// The day's untracked gaps (at least <see cref="MinimumGapDuration"/>), ordered
    /// by start; the view draws them as bands with a Book button.
    /// </summary>
    public IReadOnlyList<CalendarGapViewModel> Gaps { get; private set; } = [];

    /// <summary>What distributing the day's untracked time over its tasks would change.</summary>
    public DistributionPlan Distribution { get; private set; } = DistributionPlan.Empty;

    /// <summary>True when some task of the day can absorb a share of the missing time.</summary>
    public bool CanDistribute => Distribution.HasWork;

    /// <summary>Tooltip for the distribute action.</summary>
    public string DistributionHint => CanDistribute
        ? Strings.Calendar_DistributionHint
        : Strings.Calendar_DistributionHintNone;

    /// <summary>Replaces the day's distribution plan.</summary>
    public void SetDistribution(DistributionPlan distribution)
    {
        Distribution = distribution;
        OnPropertyChanged(nameof(Distribution));
        OnPropertyChanged(nameof(CanDistribute));
    }

    /// <summary>Replaces the day's gap bands, ordered by start.</summary>
    public void SetGaps(IEnumerable<TimeRange> gaps)
    {
        Gaps = [.. gaps.Select(g => new CalendarGapViewModel(g))];
        OnPropertyChanged(nameof(Gaps));
    }

    /// <summary>Replaces the day's activity strip spans, clipped to the day.</summary>
    public void SetActiveSpans(IEnumerable<TimeRange> spans)
    {
        ActiveSpans = [.. spans.Where(s => s.Duration > TimeSpan.Zero).OrderBy(s => s.Start)];
        OnPropertyChanged(nameof(ActiveSpans));
    }

    /// <summary>Applies one built day: sessions, activity spans, gaps and distribution.</summary>
    public void Apply(CalendarDayContent content)
    {
        Update(content.Date, content.Sessions);
        ContributorText = content.ContributorText;
        ActiveTime = content.ActiveTime;
        SetActiveSpans(content.ActiveSpans);
        SetDistribution(content.Distribution);
        SetGaps(content.Gaps);
    }

    /// <summary>Replaces the day's clipped session blocks.</summary>
    public void Update(DateTimeOffset date, IEnumerable<CalendarSessionViewModel> sessions)
    {
        var items = sessions.OrderBy(s => s.Start).ToList();

        Date = date;
        Header = $"{date.ToString("ddd", CultureInfo.InvariantCulture)} {date.Day:00}.{date.Month:00}.";

        Sessions = items;
        CopyGroups = BuildCopyGroups(items);
        _bookedSeconds = items.Sum(s => s.Duration.TotalSeconds);
        TotalText = items.Count > 0 ? $"Σ {CalendarTimeFormat.HoursMinutes(_bookedSeconds)}" : "";
        IsToday = date.Date == _now().Date;
        RefreshUntracked();

        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(CopyGroups));
    }

    /// <summary>
    /// One group per trimmed booking element (case-insensitive; entries without
    /// one form the &lt;None&gt; group), ordered by earliest session. Each group
    /// carries the clipped day time and its distinct task names in first-seen
    /// order.
    /// </summary>
    private static IReadOnlyList<CalendarCopyGroupViewModel> BuildCopyGroups(
        IReadOnlyList<CalendarSessionViewModel> sessions)
    {
        var groups = sessions
            .GroupBy(session => session.BookingElement.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Min(session => session.Start))
            .ThenBy(group => ElementLabel(group.Key), StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new CalendarCopyGroupViewModel(
                ElementLabel(group.Key),
                group.Aggregate(TimeSpan.Zero, (sum, session) => sum + session.Duration),
                [.. group
                    .GroupBy(session => session.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(task => task.Min(session => session.Start))
                    .ThenBy(task => task.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(task => task.Key)]))
            .ToList();

        IReadOnlyList<CalendarCopyGroupViewModel> result = groups;
        return result;
    }

    private static string ElementLabel(string element) =>
        element.Length > 0 ? element : Strings.Calendar_NoBookingElement;

    /// <summary>Recomputes the untracked time from the active time and the booked sessions.</summary>
    private void RefreshUntracked()
    {
        var untracked = _activeTime - TimeSpan.FromSeconds(_bookedSeconds);
        _untrackedTime = untracked > TimeSpan.Zero ? untracked : TimeSpan.Zero;
        if (untracked < MinimumUntrackedTime)
        {
            IsUntrackedWarning = false;
            UntrackedText = "";
            return;
        }

        IsUntrackedWarning = untracked >= UntrackedWarningThreshold;
        var prefix = IsUntrackedWarning ? "⚠ " : "";
        UntrackedText = prefix + string.Format(Strings.Calendar_DistributeUntracked, CalendarTimeFormat.HoursMinutes(untracked.TotalSeconds));
    }
}
