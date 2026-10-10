using Timetracker.Plugins.ActivityMonitor.Interfaces;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>In-memory installer so tests never touch the real HKCU Run key.</summary>
internal sealed class InMemoryInstaller : IActivityMonitorInstaller
{
    public string MonitorExePath => Path.Combine(
        Path.GetTempPath(), "Timetracker.Plugins.ActivityMonitor");

    public bool IsInstalled { get; set; }

    public bool Install()
    {
        IsInstalled = true;
        return true;
    }

    public bool Uninstall()
    {
        IsInstalled = false;
        return true;
    }
}
