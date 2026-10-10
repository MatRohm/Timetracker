namespace Timetracker.App.Services;

internal sealed class LegacySessionSpan
{
    public Guid Id { get; set; }

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public string Duration { get; set; } = "";

    public double DurationSeconds { get; set; }
}
