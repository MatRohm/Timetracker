namespace Timetracker.App.Services;

/// <summary>Outcome kind of an <see cref="EntryEditor"/> operation.</summary>
public enum EntryEditStatus
{
    /// <summary>Nothing to do: there were no sessions or changes.</summary>
    Declined,

    /// <summary>The change was persisted.</summary>
    Saved,

    /// <summary>Persisting failed; the file was left untouched.</summary>
    Failed,
}
