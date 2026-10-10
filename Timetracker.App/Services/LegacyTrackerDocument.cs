namespace Timetracker.App.Services;

/// <summary>The version-2/version-3 file shape: sessions still carry start, end and duration.</summary>
internal sealed class LegacyTrackerDocument
{
    public int Version { get; set; }

    public List<LegacyTrackedTask> Tasks { get; set; } = [];
}
