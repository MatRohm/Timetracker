using Avalonia.Threading;
using Timetracker.Plugins.ActivityMonitor.Interfaces;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// Stops the tracker's running session after a long idle stretch, back-dating the
/// end to the last input so idle time is not billed to the task. Owns the idle
/// auto-stop rule that used to live in the tracker view model; runs as an
/// application hook, polling the idle state on a UI-thread timer.
/// </summary>
/// <param name="now">Current time; replaceable so back-dating can be tested deterministically.</param>
/// <param name="options">The user's options; null keeps the built-in threshold.</param>
public sealed class IdleAutoStop(
    ITrackerSessionQuery sessionQuery,
    ITrackerSessionCommand sessionCommand,
    IIdleTimeProvider idleTime,
    Func<DateTimeOffset>? now = null,
    IOptionQuery? options = null) : IAppCommand, IDisposable
{
    /// <summary>No keyboard/mouse input for this long stops the running session.</summary>
    public static readonly TimeSpan IdleStopThreshold = TimeSpan.FromMinutes(30);

    /// <summary>How often the idle state is polled while the app runs.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly ITrackerSessionQuery _sessionQuery = sessionQuery;
    private readonly ITrackerSessionCommand _sessionCommand = sessionCommand;
    private readonly IIdleTimeProvider _idleTime = idleTime;
    private readonly IOptionQuery? _options = options;
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.Now);

    private DispatcherTimer? _timer;

    public void OnAppStarted()
    {
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();
    }

    public void OnAppClosing() => Dispose();

    public void Dispose() => _timer?.Stop();

    /// <summary>
    /// Polls the idle state: when a session runs and the machine has been idle for
    /// the threshold, stops it and back-dates the end to the last input.
    /// </summary>
    public async Task PollAsync()
    {
        if (!_sessionQuery.IsSessionRunning)
        {
            return;
        }

        var idle = _idleTime.CurrentIdleTime;
        if (idle is null)
        {
            // Idle cannot be measured: do not auto-stop (previously zero idle, which
            // never crossed the threshold, produced the same outcome).
            return;
        }

        var known = idle.Value;
        var threshold = _options is null
            ? IdleStopThreshold
            : IdleOptions.IdleStopThreshold(_options.GetValue(IdleOptions.IdleStopThresholdKey));
        if (known < threshold)
        {
            return;
        }

        var endedAt = _now() - known;
        await _sessionCommand.StopSessionAsync(endedAt, $"⏸ Stopped after {FormatIdle(known)} idle.");
    }

    private static string FormatIdle(TimeSpan idle)
    {
        var minutes = (int)Math.Round(idle.TotalMinutes);
        var result = minutes == 1 ? "1 min" : $"{minutes} min";
        return result;
    }
}
