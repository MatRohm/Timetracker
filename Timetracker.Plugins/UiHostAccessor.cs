namespace Timetracker.Plugins;

/// <summary>
/// Holds the UI host implementations. The tab views register themselves when
/// they are created; add-in components resolve the hosts through the DI
/// container, which forwards to this accessor. This avoids a reference from the
/// add-ins to the main app project.
/// </summary>
public static class UiHostAccessor
{
    private static volatile object? _trackerHost;
    private static volatile Action<string, bool>? _weekStatusSink;

    /// <summary>Called by the tracker tab view when it is created.</summary>
    public static void RegisterTrackerHost(object host) =>
        _trackerHost = host ?? throw new ArgumentNullException(nameof(host));

    /// <summary>Called by the week tab view when it is created.</summary>
    public static void RegisterWeekStatusSink(Action<string, bool> sink) =>
        _weekStatusSink = sink ?? throw new ArgumentNullException(nameof(sink));

    /// <summary>
    /// The tracker host registered by the view; typed lazily so this project does
    /// not need the interface definition of the add-in.
    /// </summary>
    public static T GetTrackerHost<T>() where T : class =>
        (_trackerHost as T) ?? throw new InvalidOperationException(
            "The tracker view has not registered a host yet.");

    /// <summary>Shows a status in the week view's shared status line (if shown).</summary>
    public static void ShowWeekStatus(string message, bool success) =>
        _weekStatusSink?.Invoke(message, success);
}
