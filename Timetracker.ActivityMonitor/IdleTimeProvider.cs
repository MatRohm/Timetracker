using System.Runtime.InteropServices;

namespace Timetracker.ActivityMonitor;

/// <summary>Supplies the time since the last user input (keyboard/mouse).</summary>
public interface IIdleTimeProvider
{
    /// <summary>Time since the last user input; <see cref="TimeSpan.Zero"/> when unknown.</summary>
    TimeSpan CurrentIdleTime { get; }
}

/// <summary>Selects the idle-time implementation for the current operating system.</summary>
public static class IdleTimeProvider
{
    /// <summary>
    /// Windows uses the Win32 last-input timestamp; Linux uses the X11
    /// XScreenSaver extension. On any platform where neither is available the
    /// monitor still records active time, it just never detects idle.
    /// </summary>
    public static IIdleTimeProvider CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsIdleTimeProvider();
        }

        if (OperatingSystem.IsLinux())
        {
            var x11 = X11IdleTimeProvider.Create();
            if (x11 is not null)
            {
                return x11;
            }
        }

        return new NullIdleTimeProvider();
    }
}

/// <summary>Reports zero idle time; used when no platform API is available.</summary>
internal sealed class NullIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan CurrentIdleTime => TimeSpan.Zero;
}

/// <summary>Win32 last-input timestamp via <c>GetLastInputInfo</c>.</summary>
internal sealed class WindowsIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan CurrentIdleTime
    {
        get
        {
            if (!WindowsNativeMethods.GetLastInputInfo(out var lastInput))
            {
                return TimeSpan.Zero;
            }
            var uptime = unchecked(Environment.TickCount64 - (long)lastInput.dwTime);
            return TimeSpan.FromMilliseconds(Math.Max(0, uptime));
        }
    }

    private static class WindowsNativeMethods
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
}

/// <summary>
/// Linux idle time via the X11 XScreenSaver extension (<c>libXss</c>), which is
/// present on virtually every X server. Returns null when the libraries or the
/// display are unavailable (e.g. a headless Wayland session), so the caller can
/// fall back instead of failing.
/// </summary>
internal sealed class X11IdleTimeProvider : IIdleTimeProvider
{
    private readonly IntPtr _display;
    private readonly IntPtr _rootWindow;

    private X11IdleTimeProvider(IntPtr display, IntPtr rootWindow)
    {
        _display = display;
        _rootWindow = rootWindow;
    }

    /// <summary>Opens the display and resolves the root window; null on failure.</summary>
    public static X11IdleTimeProvider? Create()
    {
        try
        {
            var display = X11NativeMethods.XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                return null;
            }

            var rootWindow = X11NativeMethods.XDefaultRootWindow(display);
            var provider = new X11IdleTimeProvider(display, rootWindow);
            return provider;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // No X11 libraries (headless server, pure Wayland): idle stays unknown.
            return null;
        }
    }

    public TimeSpan CurrentIdleTime
    {
        get
        {
            var info = X11NativeMethods.XScreenSaverAllocInfo();
            if (info == IntPtr.Zero)
            {
                return TimeSpan.Zero;
            }

            try
            {
                if (X11NativeMethods.XScreenSaverQueryInfo(_display, _rootWindow, info) == 0)
                {
                    return TimeSpan.Zero;
                }

                var screenSaverInfo = Marshal.PtrToStructure<XScreenSaverInfo>(info);
                return TimeSpan.FromMilliseconds(screenSaverInfo.Idle);
            }
            finally
            {
                X11NativeMethods.XFree(info);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XScreenSaverInfo
    {
        public IntPtr Window;      // XID
        public int State;
        public int Kind;
        public nuint TilOrSince;   // unsigned long
        public nuint Idle;         // unsigned long, milliseconds
        public nuint EventMask;    // unsigned long
    }

    private static class X11NativeMethods
    {
        private const string X11 = "libX11.so.6";
        private const string Xss = "libXss.so.1";

        [DllImport(X11)]
        internal static extern IntPtr XOpenDisplay(string? displayName);

        [DllImport(X11)]
        internal static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(X11)]
        internal static extern int XFree(IntPtr data);

        [DllImport(Xss)]
        internal static extern IntPtr XScreenSaverAllocInfo();

        [DllImport(Xss)]
        internal static extern int XScreenSaverQueryInfo(
            IntPtr display, IntPtr drawable, IntPtr info);
    }
}
