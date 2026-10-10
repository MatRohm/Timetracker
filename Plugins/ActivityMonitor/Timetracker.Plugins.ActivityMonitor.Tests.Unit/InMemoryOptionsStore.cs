using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>Options kept in memory; tests set <see cref="Values"/> directly.</summary>
internal sealed class InMemoryOptionsStore : IOptionQuery
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetValue(string key) => Values.TryGetValue(key, out var value) ? value : null;
}
