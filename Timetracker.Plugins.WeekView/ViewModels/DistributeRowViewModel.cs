using Timetracker.Plugins.Contracts.ViewModels.Mvvm;

namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// One task line of the distribute dialog: the task's current day total, its
/// assigned extra share and the −/+ steppers that change the share in 15-minute
/// steps. The steppers run exactly the actions passed in; the row only carries
/// display state, refreshed by <see cref="DistributeDayViewModel"/> via
/// <see cref="SetState"/> when the shares or the placeability change.
/// </summary>
public sealed class DistributeRowViewModel : ObservableObject
{
    internal DistributeRowViewModel(
        string task,
        TimeSpan current,
        Action increase,
        Action decrease,
        Func<bool> canIncrease,
        Func<bool> canDecrease)
    {
        Task = task;
        Current = current;
        CurrentText = WeekTimeFormat.HoursMinutes(current);
        IncreaseCommand = new RelayCommand(increase, canIncrease);
        DecreaseCommand = new RelayCommand(decrease, canDecrease);
    }

    /// <summary>Task name, e.g. "Report".</summary>
    public string Task { get; }

    /// <summary>The task's current day total as "h:mm", e.g. "1:00".</summary>
    public string CurrentText { get; }

    /// <summary>The assigned extra share as "+0:30"; empty when the share is zero.</summary>
    public string ShareText { get; private set; } = "";

    /// <summary>The expected total with the share applied, e.g. "1:30"; empty while blocked.</summary>
    public string TargetText { get; private set; } = "";

    /// <summary>Why the assigned share cannot be placed; empty when the share is placeable.</summary>
    public string BlockedText { get; private set; } = "";

    /// <summary>True when the assigned share cannot be placed next to the task's last session.</summary>
    public bool IsBlocked => BlockedText.Length > 0;

    /// <summary>The planned session changes of this row, e.g. "09:00–10:00 → 09:00–10:30"; tooltip text.</summary>
    public string ChangeText { get; private set; } = "";

    public RelayCommand IncreaseCommand { get; }

    public RelayCommand DecreaseCommand { get; }

    internal TimeSpan Share { get; private set; }

    internal TimeSpan Current { get; }

    /// <summary>True when the row's + step may run; a − step may run while the share is positive.</summary>
    internal bool CanIncrease { get; private set; }

    /// <summary>Sets just the row's share (no display recompute); used while staging new shares.</summary>
    internal void SetShare(TimeSpan share)
    {
        Share = share;
    }

    /// <summary>Replaces the row's display state from the dialog view model's recompute.</summary>
    internal void SetState(TimeSpan share, bool canIncrease, string changeText, string blockedText)
    {
        CanIncrease = canIncrease;
        Share = share;
        ShareText = share > TimeSpan.Zero ? $"+{WeekTimeFormat.HoursMinutes(share)}" : "";
        TargetText = blockedText.Length == 0 && share > TimeSpan.Zero
            ? WeekTimeFormat.HoursMinutes(Current + share)
            : "";
        ChangeText = changeText;
        BlockedText = blockedText;
        OnPropertyChanged(nameof(ShareText));
        OnPropertyChanged(nameof(TargetText));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(BlockedText));
        OnPropertyChanged(nameof(IsBlocked));
        RaiseCommandsChanged();
    }

    /// <summary>Flags the commands' CanExecute as changed after a recompute.</summary>
    internal void RaiseCommandsChanged()
    {
        IncreaseCommand.RaiseCanExecuteChanged();
        DecreaseCommand.RaiseCanExecuteChanged();
    }
}

