namespace Timetracker.App.Services;

internal sealed class LegacyTrackedTask
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string BookingElement { get; set; } = "";

    public List<LegacySessionSpan> Sessions { get; set; } = [];
}
