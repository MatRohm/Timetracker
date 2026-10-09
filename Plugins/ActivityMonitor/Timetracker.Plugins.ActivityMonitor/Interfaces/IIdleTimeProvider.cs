namespace Timetracker.Plugins.ActivityMonitor.Interfaces;

/// <summary>Supplies the time since the last user input (keyboard/mouse).</summary>
public interface IIdleTimeProvider
{
    /// <summary>Time since the last user input; <c>null</c> when idle cannot be measured.</summary>
    TimeSpan? CurrentIdleTime { get; }
}
