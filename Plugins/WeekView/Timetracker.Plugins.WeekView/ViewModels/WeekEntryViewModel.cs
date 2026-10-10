namespace Timetracker.Plugins.WeekView.ViewModels;

/// <summary>
/// One task node in the week tree: a task worked on the day, merged across its
/// entries, with the summed time. The leaf of the tree (not collapsible).
/// </summary>
public sealed class WeekEntryViewModel(string task, double durationSeconds)
{

    /// <summary>Task name, e.g. "Report".</summary>
    public string Task { get; } = task;

    /// <summary>Summed time of this task's entries for the day, in seconds.</summary>
    public double DurationSeconds { get; } = durationSeconds;

    /// <summary>Summed time of this task's entries, e.g. "1:00".</summary>
    public string TotalText { get; } = WeekTimeFormat.HoursMinutes(durationSeconds);

    /// <summary>Copy text for the entry's copy button: the task name only.</summary>
    public string CopyText => Task;
}
