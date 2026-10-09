using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.WeekView.Localization;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;
using Timetracker.Plugins.WeekView.Services;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// State of the "distribute untracked time" dialog: one row per tracked task of
/// the day with −/+ steppers that move 15-minute steps of the day's untracked
/// time between the rows. The rows start from a proportional prefill; a share
/// that cannot be placed next to its last session starts at zero with a reason.
/// The caller saves <see cref="Plan"/> once the user accepts; the plan carries
/// every assigned share as session changes.
/// </summary>
public sealed class DistributeDayViewModel : ObservableObject
{
    /// <summary>The step by which the steppers move time between the rows.</summary>
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    private static readonly string BlockedReason = Strings.Week_DistributeBlocked;

    private readonly IReadOnlyList<TrackedSession> _allSessions;
    private readonly TimeRange? _running;
    private readonly TimeSpan _missing;
    private readonly List<DistributeRowViewModel> _rows;
    private readonly IReadOnlyList<TrackedSession> _daySessions;

    public DistributeDayViewModel(
        string dayHeader,
        TimeSpan missing,
        IReadOnlyList<TrackedSession> daySessions,
        IReadOnlyList<TrackedSession> allSessions,
        TimeRange? running)
    {
        DayHeader = dayHeader;
        _missing = missing;
        _daySessions = daySessions;
        _allSessions = allSessions;
        _running = running;

        _rows = [.. CreateRows(daySessions)];
        Remaining = missing;
        MissingText = missing > TimeSpan.Zero
            ? string.Format(Strings.Week_DistributeUntracked, WeekTimeFormat.HoursMinutes(missing))
            : "";
        RemainingText = RemainingTextFor(Remaining);
        Plan = DistributionPlan.Empty;

        // The proportional prefill on the step grid is staged and planned at once;
        // placement then decides which shares survive (a share that cannot be
        // placed is reset to zero, and its row shows the reason).
        List<AssignedShare> prefill = [.. DayDistribution.ProportionalShares(daySessions, missing, Step)];
        SetRowShares(prefill);
    }

    /// <summary>Snaps every placed share onto the rows' display state; blocked rows show their reason.</summary>
    private void ApplyPlan(List<AssignedShare> requested)
    {
        // The rows' staged shares are already placed-share-aligned (a blocked share
        // was reset to zero in SetRowShares), so the probes must build on them,
        // not on the pre-reset request.
        List<AssignedShare> current = [.. _rows.Select(r => new AssignedShare(r.Task, r.Share))];
        foreach (var row in _rows)
        {
            var placed = Plan.Tasks.FirstOrDefault(t => t.Name.Equals(row.Task, StringComparison.CurrentCultureIgnoreCase));
            var wanted = requested.FirstOrDefault(s => s.Task.Equals(row.Task, StringComparison.CurrentCultureIgnoreCase));
            var share = placed?.Share ?? TimeSpan.Zero;
            var blocked = wanted is { Share.Ticks: > 0 } && placed is null ? BlockedReason : "";

            // Headroom probe: may this row grow by one step (others unchanged)?
            // A probe that places the grown share means yes; together with the check
            // against Remaining the day never overbooks its untracked time.
            List<AssignedShare> grown = [.. current.Where(s => !s.Task.Equals(row.Task, StringComparison.CurrentCultureIgnoreCase)),
                new AssignedShare(row.Task, share + Step)];
            var probe = DayDistribution.Plan(grown, _daySessions, _allSessions, _running);
            var canIncrease = Remaining >= Step
                && probe.Tasks.Any(t => t.Name.Equals(row.Task, StringComparison.CurrentCultureIgnoreCase));

            row.SetState(
                share,
                canIncrease,
                placed is null ? "" : string.Join("; ", placed.Changes.Select(WeekTimeFormat.Describe)),
                blocked);
        }
    }

    public string DayHeader { get; }

    /// <summary>One row per tracked task, in plan order (earliest session first).</summary>
    public IReadOnlyList<DistributeRowViewModel> Rows => _rows;

    /// <summary>The day's missing time the rows share out, e.g. "1:30 untracked".</summary>
    public string MissingText { get; }

    /// <summary>The not-yet-assigned time, e.g. "left 0:30".</summary>
    public string RemainingText { get; private set; }

    /// <summary>Time not assigned to any row yet.</summary>
    public TimeSpan Remaining { get; private set; }

    /// <summary>The current plan of session changes; empty when nothing is assigned.</summary>
    public DistributionPlan Plan { get; private set; }

    /// <summary>True when the user can accept: something is planned. A row whose share
    /// could not be placed was reset to zero, so it blocks nothing.</summary>
    public bool CanAccept => Plan.Changes.Count > 0;

    private List<DistributeRowViewModel> CreateRows(IReadOnlyList<TrackedSession> daySessions)
    {
        List<DistributeRowViewModel> rows = [.. DayDistribution.GroupTasks(daySessions).Select(CreateRow)];
        return rows;
    }

    private DistributeRowViewModel CreateRow(DayTask task)
    {
        DistributeRowViewModel? result = null;
        result = new DistributeRowViewModel(
            task.Name,
            task.Total,
            increase: () => Increase(result!),
            decrease: () => Decrease(result!),
            canIncrease: () => result!.CanIncrease,
            canDecrease: () => result!.Share > TimeSpan.Zero);
        return result;
    }

    /// <summary>+ on a row assigns it one more step of the day's remaining time.</summary>
    internal void Increase(DistributeRowViewModel row)
    {
        if (!row.CanIncrease)
        {
            return;
        }

        List<AssignedShare> shares = [.. _rows.Select(r => new AssignedShare(
            r.Task, ReferenceEquals(r, row) ? r.Share + Step : r.Share))];
        SetRowShares(shares);
    }

    /// <summary>− on a row hands one step back to the day's remaining time.</summary>
    internal void Decrease(DistributeRowViewModel row)
    {
        if (row.Share <= TimeSpan.Zero)
        {
            return;
        }

        List<AssignedShare> shares = [.. _rows.Select(r => new AssignedShare(
            r.Task, ReferenceEquals(r, row) ? r.Share - Step : r.Share))];
        SetRowShares(shares);
    }

    private void SetRowShares(List<AssignedShare> shares)
    {
        foreach (var share in shares)
        {
            var row = _rows.FirstOrDefault(r => r.Task.Equals(share.Task, StringComparison.CurrentCultureIgnoreCase));
            if (row is not null)
            {
                row.SetShare(share.Share);
            }
        }

        // Planning decides which shares can be placed; a share that cannot is reset
        // to zero here, so the remaining time (and every row probe) sees it as freed.
        Plan = DayDistribution.Plan(shares, _daySessions, _allSessions, _running);
        foreach (var row in _rows)
        {
            var placed = Plan.Tasks.FirstOrDefault(t => t.Name.Equals(row.Task, StringComparison.CurrentCultureIgnoreCase));
            var wanted = shares.FirstOrDefault(s => s.Task.Equals(row.Task, StringComparison.CurrentCultureIgnoreCase));
            row.SetShare(placed?.Share ?? wanted?.Share ?? TimeSpan.Zero);
        }

        // The remaining time before the row probes: they must never assign more
        // than the day's unassigned time.
        var assigned = TimeSpan.Zero;
        foreach (var row in _rows)
        {
            assigned += row.Share;
        }
        Remaining = _missing - assigned;

        ApplyPlan(shares);

        RemainingText = RemainingTextFor(Remaining);
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(CanAccept));
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(RemainingText));
    }

    private static string RemainingTextFor(TimeSpan remaining) => string.Format(Strings.Week_DistributeLeft, WeekTimeFormat.HoursMinutes(remaining));
}

