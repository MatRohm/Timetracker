using System.Runtime.InteropServices;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Detects user activity transitions: active → idle after the threshold without
/// input, idle → active on any input. Short idle periods below the threshold are
/// ignored entirely.
/// </summary>
public sealed class IdleDetector
{
    public static readonly TimeSpan Threshold = ActivityTracker.DefaultIdleThreshold;

    private readonly TimeSpan _idleThreshold;

    public IdleDetector(TimeSpan idleThreshold)
    {
        if (idleThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleThreshold));
        }
        _idleThreshold = idleThreshold;
    }

    /// <summary>Time since the last user input (keyboard/mouse), via Win32.</summary>
    public TimeSpan CurrentIdleTime
    {
        get
        {
            if (!NativeMethods.GetLastInputInfo(out var lastInput))
            {
                return TimeSpan.Zero;
            }
            var uptime = unchecked(Environment.TickCount64 - (long)lastInput.dwTime);
            return TimeSpan.FromMilliseconds(Math.Max(0, uptime));
        }
    }

    /// <summary>
    /// Computes the next state transition from the current idle time. Returns the
    /// kind of transition ("idle", "active") and the moment it happened (which may
    /// lie in the past: the idle start is back-dated to the last input time).
    /// </summary>
    public Transition Evaluate(DateTimeOffset now, TimeSpan currentIdleTime)
    {
        if (currentIdleTime >= _idleThreshold)
        {
            // The machine went idle the moment the last input happened.
            var idleStart = now - currentIdleTime;
            return new Transition("idle", idleStart);
        }

        return new Transition("active", now);
    }

    public readonly record struct Transition(string Kind, DateTimeOffset Moment);
}

/// <summary>Win32 interop for the last-input timestamp.</summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetLastInputInfo(out LASTINPUTINFO plii);
}
