using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

/// <summary>Options kept in memory; tests set <see cref="Values"/> directly.</summary>
internal sealed class InMemoryOptionsStore : IOptionQuery, IOptionCommand
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetValue(string key) => Values.GetValueOrDefault(key);

    public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            Values.Remove(key);
        }
        else
        {
            Values[key] = value;
        }
        return Task.CompletedTask;
    }
}
