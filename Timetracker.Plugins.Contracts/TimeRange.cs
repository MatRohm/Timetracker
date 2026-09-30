namespace Timetracker.Plugins.Contracts;

/// <summary>
/// A stretch of time between two instants, e.g. a period of PC activity or an
/// untracked gap in the week view.
/// </summary>
public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    /// <summary>Length of the range; zero when the end does not lie after the start.</summary>
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
}
