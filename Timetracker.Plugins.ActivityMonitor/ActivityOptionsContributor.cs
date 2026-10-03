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
        new(
            IdleOptions.IdleSpanThresholdKey,
            "Idle span threshold (minutes)",
            OptionKind.Text,
            "60",
            HintText: "Idle stretches shorter than this are not written to the activity log; raise it to ignore short breaks, lower it to log them"),
        new(
            IdleOptions.IdleStopThresholdKey,
            "Idle stop threshold (minutes)",
            OptionKind.Text,
            "30",
            HintText: "A running session with no input for this long is stopped and back-dated to the last input, so the idle time is not billed to the task"),
        new(
            "ActivityMonitor.ActivityFile",
            "Activity file",
            OptionKind.Path,
            log.FilePath,
            IsReadOnly: true,
            HintText: "The recorded active/idle spans are appended to this JSON file."),
        new(
            "ActivityMonitor.StateFile",
            "State file",
            OptionKind.Path,
            ActivityTracker.StateFilePath,
            IsReadOnly: true,
            HintText: "The monitor's last state is kept here so an open span is closed after a restart."),
    ];
}
