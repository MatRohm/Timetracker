using System.Runtime.InteropServices;
using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

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

    public TimeSpan? CurrentIdleTime
    {
        get
        {
            var info = X11NativeMethods.XScreenSaverAllocInfo();
            if (info == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                if (X11NativeMethods.XScreenSaverQueryInfo(_display, _rootWindow, info) == 0)
                {
                    return null;
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
