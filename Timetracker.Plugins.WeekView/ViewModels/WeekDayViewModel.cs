using System.Globalization;
using System.Windows.Input;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;
using Timetracker.Plugins.WeekView.Services;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// One day node in the week tree: the weekday caption, the day's summed time and
/// its booking element groups (one per element, plus a "&lt;None&gt;" group for
/// entries without one). The instance is updated in place on rebuild, and the
/// user's expansion state is kept across those updates.
/// </summary>
public sealed class WeekDayViewModel : ObservableObject
{
    /// <summary>Group name used for entries that carry no booking element.</summary>
    public const string NoBookingElementLabel = "<None>";

    /// <summary>Untracked time from which the day row warns (highlighted, "⚠").</summary>
    public static readonly TimeSpan UntrackedWarningThreshold = TimeSpan.FromMinutes(15);

    /// <summary>Shortest untracked stretch listed as a gap row in the expanded day.</summary>
    public static readonly TimeSpan MinimumGapDuration = TimeSpan.FromMinutes(15);

    /// <summary>Gaps shorter than this are rounding noise and not shown at all.</summary>
    private static readonly TimeSpan MinimumUntrackedTime = TimeSpan.FromMinutes(1);

    private string _header = "";
    private string _totalText = "";
    private bool _isToday;
    private bool _isExpanded;
    private string _contributorText = "";
    private double _bookedSeconds;
    private TimeSpan _activeTime;
    private TimeSpan _untrackedTime;
    private string _untrackedText = "";
    private bool _isUntrackedWarning;

    public WeekDayViewModel()
    {
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public DateTimeOffset Date { get; private set; }

    /// <summary>Column caption, e.g. "Mon 14.09.".</summary>
    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    /// <summary>Sum of the day's durations, e.g. "Σ 2:30".</summary>
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

    /// <summary>True while the day shows its booking element groups.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
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

    /// <summary>Expands or collapses the day.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>
    /// The day's booking element groups, ordered by earliest start. Each carries
    /// the element's summed time and its merged tasks. Empty when the day has none.
    /// </summary>
    public IReadOnlyList<WeekElementGroupViewModel> Groups { get; private set; } = [];

    /// <summary>
    /// The day's untracked gaps (at least <see cref="MinimumGapDuration"/>), ordered
    /// by start; shown below the groups when the day is expanded.
    /// </summary>
    public IReadOnlyList<WeekGapViewModel> Gaps { get; private set; } = [];

    /// <summary>What rounding the day's task totals to half hours would change.</summary>
    public RoundingPlan Rounding { get; private set; } = RoundingPlan.Empty;

    /// <summary>True when some task of the day is not on a half hour.</summary>
    public bool CanRound => Rounding.HasWork;

    /// <summary>Tooltip for the round action.</summary>
    public string RoundingHint => CanRound
        ? "Round each task's time on this day to the nearest half hour"
        : "Every task on this day is already on a half hour";

    /// <summary>
    /// One confirmation line per rounded task, e.g.
    /// "Report 0:50 → 1:00: 10:00–10:50 → 10:00–11:00" (a removed session reads
    /// "11:00–11:10 removed").
    /// </summary>
    public IReadOnlyList<string> RoundingLines =>
        [.. Rounding.Tasks.Select(t =>
            $"{t.Name} {WeekTimeFormat.HoursMinutes(t.Total)} → {WeekTimeFormat.HoursMinutes(t.Target)}: "
            + string.Join("; ", t.Changes.Select(DescribeChange)))];

    /// <summary>One line per task that could not be rounded, with the reason.</summary>
    public IReadOnlyList<string> RoundingSkippedLines =>
        [.. Rounding.Skipped.Select(t =>
            $"{t.Name} {WeekTimeFormat.HoursMinutes(t.Total)} (no free time next to its last session)")];

    /// <summary>Replaces the day's rounding plan.</summary>
    public void SetRounding(RoundingPlan rounding)
    {
        Rounding = rounding;
        OnPropertyChanged(nameof(Rounding));
        OnPropertyChanged(nameof(CanRound));
    }

    /// <summary>What distributing the day's untracked time over its tasks would change.</summary>
    public DistributionPlan Distribution { get; private set; } = DistributionPlan.Empty;

    /// <summary>True when some task of the day can absorb a share of the missing time.</summary>
    public bool CanDistribute => Distribution.HasWork;

    /// <summary>Tooltip for the distribute action.</summary>
    public string DistributionHint => CanDistribute
        ? "Distribute the day's untracked time over its tasks in proportion to their durations"
        : "Nothing to distribute: the day has no untracked time, or no task can absorb it";

    /// <summary>
    /// One confirmation line per task that gets a share, e.g.
    /// "Report 1:00 → 1:30: 09:00–10:00 → 09:00–10:30".
    /// </summary>
    public IReadOnlyList<string> DistributionLines =>
        [.. Distribution.Tasks.Select(t =>
            $"{t.Name} {WeekTimeFormat.HoursMinutes(t.Total)} → {WeekTimeFormat.HoursMinutes(t.Total + t.Share)}: "
            + string.Join("; ", t.Changes.Select(DescribeChange)))];

    /// <summary>One line per task that could not take a share, with the reason.</summary>
    public IReadOnlyList<string> DistributionSkippedLines =>
        [.. Distribution.Skipped.Select(t =>
            $"{t.Name} {WeekTimeFormat.HoursMinutes(t.Total)} (no free time next to its last session)")];

    /// <summary>Replaces the day's distribution plan.</summary>
    public void SetDistribution(DistributionPlan distribution)
    {
        Distribution = distribution;
        OnPropertyChanged(nameof(Distribution));
        OnPropertyChanged(nameof(CanDistribute));
    }

    private static string DescribeChange(SessionChange change)
    {
        var original = WeekTimeFormat.ClockRange(change.Original.Start, change.Original.End);
        var result = change.Updated is { } updated
            ? $"{original} → {WeekTimeFormat.ClockRange(updated.Start, updated.End)}"
            : $"{original} removed";
        return result;
    }

    /// <summary>
    /// Replaces the day's gap rows, ordered by start; shown below the groups when
    /// the day is expanded.
    /// </summary>
    public void SetGaps(IEnumerable<TimeRange> gaps)
    {
        Gaps = [.. gaps.Select(g => new WeekGapViewModel(g))];
        OnPropertyChanged(nameof(Gaps));
    }

    public void Update(DateTimeOffset date, IEnumerable<TrackedSession> sessions)
    {
        var items = sessions.OrderBy(e => e.Start).ToList();

        Date = date;
        Header = $"{date.ToString("ddd", CultureInfo.InvariantCulture)} {date.Day:00}.{date.Month:00}.";

        Groups = BuildGroups(items);
        _bookedSeconds = items.Sum(e => e.DurationSeconds);
        TotalText = items.Count > 0 ? $"Σ {WeekTimeFormat.HoursMinutes(_bookedSeconds)}" : "";
        IsToday = date.Date == DateTimeOffset.Now.Date;
        RefreshUntracked();

        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(Groups));
    }

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
        UntrackedText = $"{prefix}{WeekTimeFormat.HoursMinutes(untracked.TotalSeconds)} untracked";
    }

    /// <summary>Collapses the day and every group below it.</summary>
    public void CollapseAll()
    {
        IsExpanded = false;
        foreach (var group in Groups)
        {
            group.IsExpanded = false;
        }
    }

    /// <summary>
    /// One group per booking element (case-insensitive; entries without one form
    /// the "&lt;None&gt;" group), each holding its tasks merged by name. Groups and
    /// tasks are ordered by earliest start, tie-broken by name.
    /// </summary>
    private IReadOnlyList<WeekElementGroupViewModel> BuildGroups(IReadOnlyList<TrackedSession> items)
    {
        // Keep the expansion of elements that are still present.
        var expanded = Groups.Where(g => g.IsExpanded).Select(g => g.Name).ToHashSet(StringComparer.Ordinal);

        return [.. items
            .GroupBy(ElementKey, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Min(e => e.Start))
            .ThenBy(g => ElementLabel(g.Key), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => Observe(new WeekElementGroupViewModel(ElementLabel(g.Key), BuildEntries(g))
            {
                IsExpanded = expanded.Contains(ElementLabel(g.Key)),
            }))];
    }

    /// <summary>
    /// Re-raises a group's changes as a change of <see cref="Groups"/>, so the view
    /// only has to observe the day node to repaint the whole subtree.
    /// </summary>
    private WeekElementGroupViewModel Observe(WeekElementGroupViewModel group)
    {
        group.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Groups));
        return group;
    }

    private static IReadOnlyList<WeekEntryViewModel> BuildEntries(IEnumerable<TrackedSession> sessions) =>
        [.. sessions
            .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Min(e => e.Start))
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new WeekEntryViewModel(g.Key, g.Sum(e => e.DurationSeconds)))];

    private static string ElementKey(TrackedSession entry) => entry.BookingElement.Trim();

    private static string ElementLabel(string element) =>
        element.Length > 0 ? element : NoBookingElementLabel;
}
