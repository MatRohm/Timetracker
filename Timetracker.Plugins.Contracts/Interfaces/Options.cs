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

/// <summary>
/// Changes the user's options, persisted per user by the main app. Components
/// resolve it via the DI container to write their settings.
/// </summary>
public interface IOptionCommand
{
    /// <summary>Stores the value and persists all options; null removes the option.</summary>
    Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contributes a section of options to the options view. The app renders every
/// option the same way (with a file-explorer button for paths) and stores the
/// edited values through the <see cref="IOptionCommand"/>.
/// </summary>
public interface IOptionDefinitionQuery
{
    /// <summary>Heading of the section, e.g. "Azure DevOps".</summary>
    string Section { get; }

    /// <summary>The options of the section, in display order.</summary>
    IReadOnlyList<OptionDefinition> Options { get; }
}
