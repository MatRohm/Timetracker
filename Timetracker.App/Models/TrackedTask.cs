namespace Timetracker.App.Models;

/// <summary>One task in the file: its name, booking element and worked sessions.</summary>
public sealed class TrackedTask
{
    /// <summary>Stable identity of the task; empty for files written before it existed.</summary>
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string BookingElement { get; set; } = "";

    public List<SessionSpan> Sessions { get; set; } = [];
}
