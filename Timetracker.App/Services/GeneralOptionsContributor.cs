using Timetracker.App.Localization;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;
using Timetracker.Plugins.Contracts.Logging;

namespace Timetracker.App.Services;

/// <summary>
/// The app's own section of the options view: where the tracked sessions, the
/// options and the logs are kept. All entries are read-only.
/// </summary>
public sealed class GeneralOptionsContributor(string trackingFilePath, string optionsFilePath, string logDirectory)
    : IOptionDefinitionQuery
{
    public GeneralOptionsContributor(JsonTrackerRepository repository, JsonOptionsStore options)
        : this(repository.FilePath, options.FilePath, LogLocation.Directory)
    {
    }

    public string Section => Strings.Options_SectionGeneral;

    public IReadOnlyList<OptionDefinition> Options { get; } =
    [
        new(
            "General.TrackingFile",
            Strings.Options_TrackingFile,
            OptionKind.Path,
            trackingFilePath,
            IsReadOnly: true,
            HintText: Strings.Options_TrackingFileHint),
        new(
            "General.OptionsFile",
            Strings.Options_OptionsFile,
            OptionKind.Path,
            optionsFilePath,
            IsReadOnly: true,
            HintText: Strings.Options_OptionsFileHint),
        new(
            "General.LogFolder",
            Strings.Options_LogFolder,
            OptionKind.Path,
            logDirectory,
            IsReadOnly: true,
            HintText: Strings.Options_LogFolderHint),
    ];
}
