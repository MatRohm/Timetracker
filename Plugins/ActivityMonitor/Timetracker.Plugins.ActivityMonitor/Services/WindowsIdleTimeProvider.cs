using System.Runtime.InteropServices;
using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>Win32 last-input timestamp via <c>GetLastInputInfo</c>.</summary>
internal sealed class WindowsIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan? CurrentIdleTime
    {
        get
        {
            if (!WindowsNativeMethods.TryGetLastInputTick(out var lastInputTick))
            {
                return null;
            }

            return TimeSpan.FromMilliseconds(IdleMilliseconds(lastInputTick, Environment.TickCount));
        }
    }

    /// <summary>
    /// Idle milliseconds between the last input and now. Both are 32-bit millisecond
    /// tick counters, so the subtraction is done in 32-bit space; that keeps the
    /// result correct when the tick count wraps (roughly every 49.7 days), which a
    /// 64-bit <c>TickCount64</c> against the 32-bit <c>dwTime</c> would not.
    /// </summary>
    internal static uint IdleMilliseconds(uint lastInputTick, int currentTick) =>
        unchecked((uint)currentTick - lastInputTick);

    /// <summary>Test seam for the Win32 call; also documents the cbSize requirement.</summary>
    internal static bool TryGetLastInputTick(out uint lastInputTick) =>
        WindowsNativeMethods.TryGetLastInputTick(out lastInputTick);

    private static class WindowsNativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>
        /// Tick count of the last input event. <c>cbSize</c> must hold the size of
        /// the structure, otherwise GetLastInputInfo fails and reports no input at
        /// all, so it is always set here (an <c>out</c> parameter would leave it 0).
        /// </summary>
        internal static bool TryGetLastInputTick(out uint lastInputTick)
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            var succeeded = GetLastInputInfo(ref info);
            lastInputTick = info.dwTime;
            return succeeded;
        }
    }
}
