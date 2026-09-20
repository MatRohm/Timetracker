using Timetracker.Plugins;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Forwards status requests from the monitor components to the application's
/// shared status line (the week view registers the sink at startup).
/// </summary>
public sealed class WeekStatusHostAdapter : IWeekStatusHost
{
    public void ShowStatus(string message, bool success) =>
        UiHostAccessor.ShowWeekStatus(message, success);
}
