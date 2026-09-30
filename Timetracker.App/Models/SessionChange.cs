namespace Timetracker.App.Models;

/// <summary>
/// A planned change to one stored session: <see cref="Original"/> is the session
/// as stored, <see cref="Updated"/> its new form, or null when it is removed.
/// </summary>
public sealed record SessionChange(TrackerEntry Original, TrackerEntry? Updated);
