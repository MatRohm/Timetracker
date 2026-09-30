namespace Timetracker.Plugins.Contracts;

/// <summary>A stretch of time in which the computer was actively used.</summary>
public sealed record ActiveSpan(DateTimeOffset Start, DateTimeOffset End);
