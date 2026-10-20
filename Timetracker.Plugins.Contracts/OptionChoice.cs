namespace Timetracker.Plugins.Contracts;

/// <summary>One allowed value of an <see cref="OptionKind.Choice"/> option.</summary>
/// <param name="Value">The stable value stored in the options file.</param>
/// <param name="Label">The user-visible text of the choice.</param>
public sealed record OptionChoice(string Value, string Label);
