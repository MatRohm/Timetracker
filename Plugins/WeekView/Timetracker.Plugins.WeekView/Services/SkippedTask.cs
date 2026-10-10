namespace Timetracker.Plugins.WeekView.Services;

/// <summary>A task that could not be adjusted because no free time borders its last session.</summary>
public sealed record SkippedTask(string Name, TimeSpan Total);
