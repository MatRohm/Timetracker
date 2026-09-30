namespace Timetracker.Plugins.Contracts;

/// <summary>
/// Stable keys of the application's tabs, shared by the shell and the add-ins so
/// a tab is addressed by name rather than by a magic string.
/// </summary>
public static class TabKeys
{
    /// <summary>The tracker tab (start/stop and the session history).</summary>
    public const string Tracker = "Tracker";

    /// <summary>The week view tab.</summary>
    public const string WeekView = "Week view";

    /// <summary>The options tab.</summary>
    public const string Options = "Options";
}
