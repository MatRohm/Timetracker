using Timetracker.Plugins.Contracts;

namespace Timetracker.Plugins.Calendar.Services;

/// <summary>A day's tracked task: its grouped name, summed total and its sessions (earliest end last).</summary>
internal readonly record struct DayTask(string Name, TimeSpan Total, IReadOnlyList<TrackedSession> Sessions);
