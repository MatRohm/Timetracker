namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// One booking element of a day, ready for the day-header copy flyout: its
/// display label with the summed time and the task names the copy action puts
/// on the clipboard, one per line.
/// </summary>
public sealed record CalendarCopyGroupViewModel(string Name, TimeSpan Total, IReadOnlyList<string> Tasks)
{
    /// <summary>Flyout label, e.g. "Project X (1:30)".</summary>
    public string Label => $"{Name} ({CalendarTimeFormat.HoursMinutes(Total)})";

    /// <summary>The element's distinct task names, one per line.</summary>
    public string CopyText => string.Join(Environment.NewLine, Tasks);
}
