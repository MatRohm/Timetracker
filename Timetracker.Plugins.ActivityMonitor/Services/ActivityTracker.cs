using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>
/// Tracks the machine state (active/idle/off) and writes one span per state
/// change to the <see cref="ActivityLog"/>: "active" spans cover user-present
/// periods, "idle" spans only periods of at least the configured idle span
/// threshold (default one hour; shorter breaks are ignored). Survives process
/// restarts via a small state file, so logoff/shutdown
/// ends the open span correctly on the next start.
/// </summary>
public class ActivityTracker
{
    /// <summary>Idle periods shorter than the configured threshold are not logged; the default.</summary>
    public static readonly TimeSpan DefaultIdleThreshold = TimeSpan.FromHours(1);

    /// <summary>How often a routine polling entry is written to the monitor log.</summary>
    public static readonly TimeSpan PollLogInterval = TimeSpan.FromMinutes(5);

    private readonly ActivityLog _log;
    private readonly IIdleTimeProvider _idleTime;
    private readonly ILogger<ActivityTracker> _logger;
    private Func<DateTimeOffset> _now;

    /// <summary>Idle stretches shorter than this are not logged; the option may lower or raise it.</summary>
    private TimeSpan _idleSpanThreshold;

    /// <summary>Moment of the last polling log entry; throttles it to <see cref="PollLogInterval"/>.</summary>
    private DateTimeOffset _lastPollLog;

    private string _state = "active";
    private DateTimeOffset _stateStart;

    public ActivityTracker(
        ActivityLog log,
        Func<DateTimeOffset>? now = null,
        IIdleTimeProvider? idleTime = null,
        ILogger<ActivityTracker>? logger = null,
        TimeSpan? idleSpanThreshold = null)
    {
        _log = log;
        _now = now ?? (() => DateTimeOffset.Now);
        _idleTime = idleTime ?? IdleTimeProvider.CreateForCurrentPlatform();
        _logger = logger ?? NullLogger<ActivityTracker>.Instance;
        _idleSpanThreshold = idleSpanThreshold ?? DefaultIdleThreshold;
    }

    /// <summary>The configured shortest logged idle stretch; tests set it directly.</summary>
    internal TimeSpan IdleSpanThreshold
    {
        get => _idleSpanThreshold;
        set => _idleSpanThreshold = value;
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

    /// <summary>Test seam: polls with an injected idle duration instead of the platform.</summary>
    protected void PollIdleForTest(TimeSpan idle) => PollWithIdle(idle);

    private void PollWithIdle(TimeSpan idle)
    {
        if (_state == "active" && idle >= _idleSpanThreshold)
        {
            var idleStart = _now() - idle;
            CloseAndOpen("idle", idleStart);
        }
        else if (_state == "idle" && idle < _idleSpanThreshold)
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
        _lastPollLog = now;
        SaveState(_state, _stateStart);

        _logger.LogInformation(
            "Started; resumed state \"{State}\", current span \"{Span}\" from {SpanStart:O}.",
            state, _state, _stateStart);
        return _stateStart;
    }

    /// <summary>Called periodically; flips active/idle when the threshold is crossed.</summary>
    public void Poll()
    {
        var now = _now();
        var idle = _idleTime.CurrentIdleTime;
        PollWithIdle(idle);

        // Routine heartbeat, throttled so the log does not grow per poll tick.
        if (now - _lastPollLog >= PollLogInterval)
        {
            _lastPollLog = now;
            _logger.LogInformation("Poll; state \"{State}\", idle {Idle:hh\\:mm\\:ss}.", _state, idle);
        }
    }

    /// <summary>Ends the open span (logoff/shutdown); called on session end.</summary>
    public void Stop()
    {
        if (_state == "off")
        {
            return; // Already stopped; avoids writing a duplicate span.
        }

        var stoppedState = _state;
        var endedAt = _now();
        CloseSpan(stoppedState, _stateStart, endedAt);
        _state = "off";
        SaveState(_state, null);

        _logger.LogInformation("Stopped; closed \"{Span}\" span at {EndedAt:O}.", stoppedState, endedAt);
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

        if (state == "idle" && end - start < _idleSpanThreshold)
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
                _logger.LogInformation("No state file at \"{Path}\"; starting fresh.", _statePath);
                return ("active", null);
            }

            var lines = File.ReadAllText(_statePath).Split('\n', 2);
            var state = lines[0].Trim();
            DateTimeOffset? start = null;
            var startText = lines.Length > 1 ? lines[1].Trim() : "";
            if (startText.Length > 0)
            {
                if (DateTimeOffset.TryParse(startText, out var parsed))
                {
                    start = parsed;
                }
                else
                {
                    _logger.LogWarning(
                        "State file \"{Path}\" held an unparsable start time; resuming without it.", _statePath);
                }
            }

            return (state, start);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not read the state file \"{Path}\"; starting fresh.", _statePath);
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
