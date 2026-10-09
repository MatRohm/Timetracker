using System.Globalization;
using System.Text.Json;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// The idle durations of the activity monitor, editable in the options tab
/// (section "Activity monitor"): the shortest idle stretch logged as an "idle"
/// span and the idle duration that stops a running session. Values are whole
/// minutes; stored under the option keys the contributor registers.
/// </summary>
public static class IdleOptions
{
    public const string IdleSpanThresholdKey = "ActivityMonitor.IdleSpanThreshold";
    public const string IdleStopThresholdKey = "ActivityMonitor.IdleStopThreshold";

    /// <summary>The same options file the app's options store writes (duplicated: no app reference).</summary>
    public static string OptionsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "timetracker-options.json");

    /// <summary>The shortest idle stretch the monitor logs as an "idle" span; default one hour.</summary>
    public static TimeSpan IdleSpanThreshold(string? value)
    {
        var result = FromMinutes(value, ActivityTracker.DefaultIdleThreshold);
        return result;
    }

    /// <summary>The idle duration that stops a running session; default thirty minutes.</summary>
    public static TimeSpan IdleStopThreshold(string? value)
    {
        var result = FromMinutes(value, IdleAutoStop.IdleStopThreshold);
        return result;
    }

    /// <summary>Option value in whole minutes; missing/blank/non-numeric/zero-or-negative falls back.</summary>
    public static TimeSpan FromMinutes(string? value, TimeSpan fallback)
    {
        var result = int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : fallback;
        return result;
    }

    /// <summary>
    /// Reads one string value from the shared options file. The headless monitor
    /// has no DI container to resolve <see cref="IOptionQuery"/> and does not
    /// reference the main app's options store, so it reads the same flat JSON
    /// object of key/value strings the app writes with a small local read.
    /// </summary>
    internal static string? ReadValueFromOptionsFile(string path, string key)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path);
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty(key, out var element) ||
                element.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var result = element.GetString();
            return result;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Same contract as the app's store: a broken file yields no options.
            return null;
        }
    }
}
