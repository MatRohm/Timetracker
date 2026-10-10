using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.WeekView.Services;

/// <summary>A task whose day total is rounded: its old total, the target and the session changes.</summary>
public sealed record RoundedTask(string Name, TimeSpan Total, TimeSpan Target, IReadOnlyList<SessionChange> Changes);
