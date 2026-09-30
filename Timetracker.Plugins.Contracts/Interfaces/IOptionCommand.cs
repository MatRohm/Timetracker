namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// Changes the user's options, persisted per user by the main app. Components
/// resolve it via the DI container to write their settings.
/// </summary>
public interface IOptionCommand
{
    /// <summary>Stores the value and persists all options; null removes the option.</summary>
    Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default);
}
