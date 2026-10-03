using Timetracker.Plugins.ActivityMonitor.Localization;
using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor;

/// <summary>
/// The "Activity monitor" section of the options tab: where the monitor keeps the
/// recorded spans and its state across restarts (read-only), plus the two idle
/// thresholds in whole minutes.
/// </summary>
public sealed class ActivityOptionsContributor(ActivityLog log) : IOptionDefinitionQuery
{
    public string Section => Strings.ActivityMon_Section;

    public IReadOnlyList<OptionDefinition> Options { get; } =
    [
        new(
            IdleOptions.IdleSpanThresholdKey,
            Strings.ActivityMon_IdleSpanLabel,
            OptionKind.Text,
            "60",
            HintText: Strings.ActivityMon_IdleSpanHint),
        new(
            IdleOptions.IdleStopThresholdKey,
            Strings.ActivityMon_IdleStopLabel,
            OptionKind.Text,
            "30",
            HintText: Strings.ActivityMon_IdleStopHint),
        new(
            "ActivityMonitor.ActivityFile",
            Strings.ActivityMon_ActivityFileLabel,
            OptionKind.Path,
            log.FilePath,
            IsReadOnly: true,
            HintText: Strings.ActivityMon_ActivityFileHint),
        new(
            "ActivityMonitor.StateFile",
            Strings.ActivityMon_StateFileLabel,
            OptionKind.Path,
            ActivityTracker.StateFilePath,
            IsReadOnly: true,
            HintText: Strings.ActivityMon_StateFileHint),
    ];
}
