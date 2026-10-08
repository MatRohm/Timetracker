using Avalonia.Controls;

namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>
/// A free-form block an add-in contributes to a section of the options view,
/// rendered below that section's value rows. The <see cref="Section"/> name
/// matches an <see cref="IOptionDefinitionQuery.Section"/> so the block joins the
/// existing heading rather than duplicating it; a section that matches no option
/// section is rendered as its own section after the option sections.
/// </summary>
public interface IOptionUiQuery
{
    /// <summary>Heading of the section the block joins, e.g. "Activity monitor".</summary>
    string Section { get; }

    /// <summary>Creates the block's content. Called once at startup.</summary>
    Control CreateControl(IServiceProvider services);
}
