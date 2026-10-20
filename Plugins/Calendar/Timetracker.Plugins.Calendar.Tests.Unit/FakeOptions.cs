using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.Calendar.Tests.Unit;

/// <summary>
/// In-memory option store for the calendar tests. <see cref="SaveThrows"/> makes
/// writes fail like an unwritable options file.
/// </summary>
public sealed class FakeOptions : IOptionQuery, IOptionCommand
{
    private readonly Dictionary<string, string> _values = [];

    /// <summary>Keys written through <see cref="IOptionCommand"/>, in order.</summary>
    public List<string> Writes { get; } = [];

    /// <summary>When true, <see cref="SetValueAsync"/> throws an I/O error.</summary>
    public bool SaveThrows { get; set; }

    public string? GetValue(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string? value)
    {
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }
    }

    public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (SaveThrows)
        {
            throw new IOException("options file is read-only");
        }

        Writes.Add(key);
        Set(key, value);
        return Task.CompletedTask;
    }
}
