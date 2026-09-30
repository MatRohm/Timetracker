namespace Timetracker.Plugins.Contracts;

/// <summary>
/// A planned change to one stored session: <see cref="Original"/> is the session
/// as stored, <see cref="Updated"/> its new form, or null when it is removed.
/// </summary>
public sealed record SessionChange(TrackedSession Original, TrackedSession? Updated);
