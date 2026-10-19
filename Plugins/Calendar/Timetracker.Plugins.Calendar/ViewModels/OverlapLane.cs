namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// Where a session block sits horizontally inside its day column: the lane index
/// and how many lanes the overlapping cluster has.
/// </summary>
public sealed record OverlapLane(int Lane, int LaneCount);
