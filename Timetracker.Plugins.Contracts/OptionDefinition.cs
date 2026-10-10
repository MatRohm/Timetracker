namespace Timetracker.Plugins.Contracts;

/// <summary>One option a component shows in the options view.</summary>
/// <param name="Key">Store key, prefixed with the component's name ("AzureDevOps.Url").</param>
/// <param name="Label">Text shown next to the value.</param>
/// <param name="Kind">How the value is edited and shown.</param>
/// <param name="DefaultValue">
/// Shown while the store holds no value for <paramref name="Key"/>. For a read-only
/// option it is the value itself, which is never stored.
/// </param>
/// <param name="IsReadOnly">True for information such as a file location the user cannot change.</param>
/// <param name="HintText">Optional hint shown as an (i) tooltip next to the label; empty means no hint.</param>
public sealed record OptionDefinition(
    string Key,
    string Label,
    OptionKind Kind,
    string DefaultValue = "",
    bool IsReadOnly = false,
    string HintText = "");
