namespace Timetracker.App.Services;

/// <summary>
/// Result of an editor operation: the outcome, a short description of what was
/// affected (e.g. the task name) and, on failure, the exception.
/// </summary>
public sealed record EntryEditResult(EntryEditStatus Status, string Summary, Exception? Error = null);
