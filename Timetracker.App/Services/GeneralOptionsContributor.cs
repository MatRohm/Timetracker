using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts.Logging;

namespace Timetracker.App.Services;

/// <summary>
/// The app's own section of the options view: where the tracked sessions, the
/// options and the logs are kept. All entries are read-only.
/// </summary>
public sealed class GeneralOptionsContributor(string trackingFilePath, string optionsFilePath, string logDirectory)
    : IOptionsContributor
{
    public GeneralOptionsContributor(JsonTrackerRepository repository, JsonOptionsStore options)
        : this(repository.FilePath, options.FilePath, LogLocation.Directory)
    {
    }

    public string Section => "General";

    public IReadOnlyList<OptionDefinition> Options { get; } =
    [
        new("General.TrackingFile", "Tracking file", OptionKind.Path, trackingFilePath, IsReadOnly: true),
        new("General.OptionsFile", "Options file", OptionKind.Path, optionsFilePath, IsReadOnly: true),
        new("General.LogFolder", "Log folder", OptionKind.Path, logDirectory, IsReadOnly: true),
    ];
}
