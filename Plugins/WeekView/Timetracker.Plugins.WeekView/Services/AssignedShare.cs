namespace Timetracker.Plugins.WeekView.Services;

/// <summary>A task's hand-assigned share of the day's missing time, e.g. from the distribute dialog.</summary>
public sealed record AssignedShare(string Task, TimeSpan Share);
