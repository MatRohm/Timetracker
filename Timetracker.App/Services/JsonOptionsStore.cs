using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.App.Services;

/// <summary>
/// Keeps the user's options in <c>~/.timetracker/timetracker-options.json</c> as one
/// flat JSON object of key/value strings. The file is read once when the store is
/// created and rewritten atomically on every change; a missing or broken file
/// yields no options, so every component falls back to its defaults.
/// </summary>
public sealed class JsonOptionsStore : IOptionQuery, IOptionCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<JsonOptionsStore> _logger;
    private readonly Dictionary<string, string> _values;

    /// <summary>Serializes the writes, so a slower write never overtakes a newer one.</summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <param name="filePath">Overrides <see cref="DefaultFilePath"/> (used by tests).</param>
    /// <param name="logger">Receives the reason the file could not be read or written.</param>
    public JsonOptionsStore(string? filePath = null, ILogger<JsonOptionsStore>? logger = null)
    {
        _path = filePath ?? DefaultFilePath;
        _logger = logger ?? NullLogger<JsonOptionsStore>.Instance;
        _values = Load();
    }

    /// <summary>Default path of the options file (the fixed anchor).</summary>
    public static string DefaultFilePath => TimetrackerPaths.OptionsFile;

    public string FilePath => _path;

    public string? GetValue(string key)
    {
        lock (_values)
        {
            var result = _values.GetValueOrDefault(key);
            return result;
        }
    }

    public async Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            string json;
            lock (_values)
            {
                if (value is null)
                {
                    _values.Remove(key);
                }
                else
                {
                    _values[key] = value;
                }

                // Sorted keys keep the file stable and easy to read by hand.
                json = JsonSerializer.Serialize(
                    new SortedDictionary<string, string>(_values, StringComparer.Ordinal), JsonOptions);
            }

            await AtomicFile.WriteAllTextAsync(_path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The value stays in effect for this session; only persisting it failed.
            _logger.LogError(ex, "Could not save the options file {Path}", _path);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var text = File.ReadAllText(_path);
            var values = string.IsNullOrWhiteSpace(text)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(text, JsonOptions);
            var result = new Dictionary<string, string>(
                values ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            return result;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Broken options must not break the app; every option falls back to its default.
            _logger.LogError(ex, "Could not load the options file {Path}", _path);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
