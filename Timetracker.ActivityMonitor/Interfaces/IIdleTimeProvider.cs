namespace Timetracker.ActivityMonitor.Interfaces;

/// <summary>Supplies the time since the last user input (keyboard/mouse).</summary>
public interface IIdleTimeProvider
{
    /// <summary>Time since the last user input; <see cref="TimeSpan.Zero"/> when unknown.</summary>
    TimeSpan CurrentIdleTime { get; }
}
