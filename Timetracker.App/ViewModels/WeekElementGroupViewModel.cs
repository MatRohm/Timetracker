using System.Windows.Input;

namespace Timetracker.ViewModels;

/// <summary>
/// One booking element node in the week tree: the element's name (or "&lt;None&gt;"
/// when the entries have no booking element), the summed time of its entries for
/// the day, and the merged tasks below it. Collapsible.
/// </summary>
public sealed class WeekElementGroupViewModel : ObservableObject
{
    private bool _isExpanded;

    public WeekElementGroupViewModel(
        string name,
        IReadOnlyList<WeekEntryViewModel> entries,
        ICommand? toggleCommand = null)
    {
        Name = name;
        Entries = entries;
        TotalText = WeekTimeFormat.HoursMinutes(entries.Sum(e => e.DurationSeconds));
        CopyText = string.Join(Environment.NewLine, entries.Select(e => e.Task));
        ToggleCommand = toggleCommand ?? new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    /// <summary>Element name as stored, or "&lt;None&gt;" when there is none.</summary>
    public string Name { get; }

    /// <summary>Merged tasks of this element for the day, oldest first.</summary>
    public IReadOnlyList<WeekEntryViewModel> Entries { get; }

    /// <summary>Summed time of the entries, e.g. "1:30".</summary>
    public string TotalText { get; }

    /// <summary>
    /// Copy text for the element's copy button: the task names of its entries, one
    /// per line (never the element name itself).
    /// </summary>
    public string CopyText { get; }

    /// <summary>True while the node shows its entries.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ICommand ToggleCommand { get; }
}
