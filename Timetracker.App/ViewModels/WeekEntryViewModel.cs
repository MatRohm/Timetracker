namespace Timetracker.App.ViewModels;

/// <summary>
/// One task node in the week tree: a task worked on the day, merged across its
/// entries, with the summed time. The leaf of the tree (not collapsible).
/// </summary>
public sealed class WeekEntryViewModel
{
    public WeekEntryViewModel(string task, double durationSeconds)
    {
        Task = task;
        DurationSeconds = durationSeconds;
        TotalText = WeekTimeFormat.HoursMinutes(durationSeconds);
    }

    /// <summary>Task name, e.g. "Report".</summary>
    public string Task { get; }

    /// <summary>Summed time of this task's entries for the day, in seconds.</summary>
    public double DurationSeconds { get; }

    /// <summary>Summed time of this task's entries, e.g. "1:00".</summary>
    public string TotalText { get; }

    /// <summary>Copy text for the entry's copy button: the task name only.</summary>
    public string CopyText => Task;
}
