using Microsoft.Win32;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Tracks the machine state (active/idle/off) and writes one span per state
/// change to the <see cref="ActivityLog"/>: "active" spans cover user-present
/// periods, "idle" spans only periods of at least one hour (shorter breaks are
/// ignored). Survives process restarts via a small state file, so logoff/shutdown
/// ends the open span correctly on the next start.
/// </summary>
public class ActivityTracker
{
    /// <summary>Idle periods shorter than this are not logged.</summary>
    public static readonly TimeSpan DefaultIdleThreshold = TimeSpan.FromHours(1);

    private const string PollIntervalSeconds = "5";

    private readonly ActivityLog _log;
    private readonly IdleDetector _detector;
    private Func<DateTimeOffset> _now;

    private string _state = "active";
    private DateTimeOffset _stateStart;

    public ActivityTracker(ActivityLog log, Func<DateTimeOffset>? now = null)
    {
        _log = log;
        _now = now ?? (() => DateTimeOffset.Now);
        _detector = new IdleDetector(DefaultIdleThreshold);
    }

    /// <summary>Path of the state file used to recover across restarts.</summary>
    public static string StateFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "timetracker-activity.state.json");

    /// <summary>Overrides the state file path (tests use per-test files).</summary>
    /// <remarks>Set right after construction, before Start/Poll.</remarks>
    public void UseStateFile(string path) => _statePath = path;

    private string _statePath = StateFilePath;

    /// <summary>Replaces the clock (tests inject fixed moments).</summary>
    protected void SetNow(Func<DateTimeOffset> now) => _now = now;

    /// <summary>Test seam: polls with an injected idle duration instead of Win32.</summary>
    protected void PollIdleForTest(TimeSpan idle) => PollWithIdle(idle);

    private void PollWithIdle(TimeSpan idle)
    {
        if (_state == "active" && idle >= IdleDetector.Threshold)
        {
            var idleStart = _now() - idle;
            CloseAndOpen("idle", idleStart);
        }
        else if (_state == "idle" && idle < IdleDetector.Threshold)
        {
            CloseAndOpen("active", _now());
        }
    }

    /// <summary>
    /// Resumes from the persisted state: if a span was open when the process or
    /// session ended, it is closed with the last known moment. Returns the moment
    /// the current span started.
    /// </summary>
    public DateTimeOffset Start()
    {
        var (state, start) = LoadState();
        var now = _now();

        if (start is not null)
        {
            // Close the span that was open before the previous run ended.
            CloseSpan(state, start.Value, now);
        }

        _state = "active";
        _stateStart = now;
        SaveState(_state, _stateStart);
        return _stateStart;
    }

    /// <summary>Called periodically; flips active/idle when the threshold is crossed.</summary>
    public void Poll() => PollWithIdle(_detector.CurrentIdleTime);

    /// <summary>Ends the open span (logoff/shutdown); called on session end.</summary>
    public void Stop()
    {
        CloseSpan(_state, _stateStart, _now());
        _state = "off";
        SaveState(_state, null);
    }

    private void CloseAndOpen(string newState, DateTimeOffset newStateStart)
    {
        CloseSpan(_state, _stateStart, newStateStart);
        _state = newState;
        _stateStart = newStateStart;
        SaveState(_state, _stateStart);
    }

    /// <summary>
    /// Writes the finished span if it is worth logging: active spans always, idle
    /// spans only when they reached the one-hour threshold.
    /// </summary>
    private void CloseSpan(string state, DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            return;
        }

        if (state == "idle" && end - start < IdleDetector.Threshold)
        {
            return; // Short break: ignore entirely.
        }

        _log.Add(state, start, end);
    }

    private (string State, DateTimeOffset? Start) LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return ("active", null);
            }
            var parts = File.ReadAllText(_statePath).Split('\n', 2);
            DateTimeOffset? start = null;
            if (parts.Length > 1 && DateTimeOffset.TryParse(parts[1].Trim(), out var parsed))
            {
                start = parsed;
            }
            return (parts[0].Trim(), start);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ("active", null);
        }
    }

    private void SaveState(string state, DateTimeOffset? start)
    {
        try
        {
            File.WriteAllText(_statePath, state + "\n" + (start?.ToString("O") ?? ""));
        }
        catch (IOException)
        {
            // Never crash the monitor over state persistence.
        }
    }
}
