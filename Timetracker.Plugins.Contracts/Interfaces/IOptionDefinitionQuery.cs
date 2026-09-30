namespace Timetracker.Plugins.Contracts.Interfaces;

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
