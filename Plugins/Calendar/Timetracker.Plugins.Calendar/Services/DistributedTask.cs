using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.Services;

/// <summary>A task whose day total grows by its proportional share of the day's missing time.</summary>
public sealed record DistributedTask(string Name, TimeSpan Total, TimeSpan Share, IReadOnlyList<SessionChange> Changes);
