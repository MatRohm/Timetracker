using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Services;

/// <summary>Selects the idle-time implementation for the current operating system.</summary>
public static class IdleTimeProvider
{
    /// <summary>
    /// Windows uses the Win32 last-input timestamp; Linux uses the X11
    /// XScreenSaver extension. On any platform where neither is available the
    /// provider reports idle as unknown (null), so the monitor records "unknown"
    /// spans that the week view omits rather than misreporting the time as active.
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
