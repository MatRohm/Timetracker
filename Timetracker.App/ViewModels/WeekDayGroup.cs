namespace Timetracker.ViewModels;

/// <summary>
/// One rendered line of a weekday column in the week view: the display label (name
/// plus total duration) and the text its copy button puts on the clipboard.
/// </summary>
public sealed class WeekDayGroup
{
    public WeekDayGroup(string label, string copyText)
    {
        Label = label;
        CopyText = copyText;
    }

    /// <summary>Line as shown in the column, e.g. "Project X (1:30)".</summary>
    public string Label { get; }

    /// <summary>Text the line's copy button copies, i.e. the line's name.</summary>
    public string CopyText { get; }
}
