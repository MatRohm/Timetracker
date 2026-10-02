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
    public string Section => "Activity monitor";

    public IReadOnlyList<OptionDefinition> Options { get; } =
    [
        new(IdleOptions.IdleSpanThresholdKey, "Idle span threshold (minutes)", OptionKind.Text, "60"),
        new(IdleOptions.IdleStopThresholdKey, "Idle stop threshold (minutes)", OptionKind.Text, "30"),
        new("ActivityMonitor.ActivityFile", "Activity file", OptionKind.Path, log.FilePath, IsReadOnly: true),
        new("ActivityMonitor.StateFile", "State file", OptionKind.Path, ActivityTracker.StateFilePath, IsReadOnly: true),
    ];
}
