namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Reads the user's options, persisted per user by the main app. Components resolve
/// it via the DI container to read their settings. Keys are prefixed with the
/// component's name ("AzureDevOps.Url"), so components never share one.
/// </summary>
public interface IOptionQuery
{
    /// <summary>The stored value; null when the option was never set.</summary>
    string? GetValue(string key);
}
