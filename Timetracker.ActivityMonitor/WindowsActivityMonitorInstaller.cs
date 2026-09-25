using Timetracker.ActivityMonitor.Interfaces;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Timetracker.ActivityMonitor;

/// <summary>
/// Installs or removes the per-user autostart of the activity monitor via the
/// HKCU Run key. A Windows service or scheduled task would require admin rights
/// and is not needed for a per-user monitor; the Run key starts the monitor at
/// every logon without elevation.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsActivityMonitorInstaller : IActivityMonitorInstaller
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TimetrackerActivityMonitor";

    /// <summary>Full path of the monitor executable next to the main app.</summary>
    public string MonitorExePath => Path.Combine(
        AppContext.BaseDirectory, "Timetracker.ActivityMonitor.exe");

    /// <summary>True when the autostart entry exists and points at this exe.</summary>
    public bool IsInstalled
    {
        get
        {
            using var key = OpenRunKey(readonlyKey: true);
            // The stored value is quoted (paths with spaces); compare unquoted.
            var value = key?.GetValue(ValueName) as string;
            var path = value?.Trim().Trim('"');
            var result = !string.IsNullOrEmpty(path)
                && string.Equals(path, MonitorExePath, StringComparison.OrdinalIgnoreCase);
            return result;
        }
    }

    /// <summary>Registers the autostart entry. Returns false when it failed.</summary>
    public bool Install()
    {
        if (!File.Exists(MonitorExePath))
        {
            return false;
        }

        try
        {
            using var key = OpenRunKey(readonlyKey: false);
            key?.SetValue(ValueName, $"\"{MonitorExePath}\"");
            var result = key is not null;
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Removes the autostart entry. Returns false when it failed.</summary>
    public bool Uninstall()
    {
        try
        {
            using var key = OpenRunKey(readonlyKey: false);
            if (key?.GetValue(ValueName) is null)
            {
                return true; // Nothing to remove counts as success.
            }
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            var result = true;
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static RegistryKey? OpenRunKey(bool readonlyKey) =>
        Registry.CurrentUser.OpenSubKey(RunKeyPath, !readonlyKey);
}
