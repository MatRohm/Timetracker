using Timetracker.App.Models;

namespace Timetracker.App.Interfaces;

/// <summary>
/// Reads and writes the tracker entries. The concrete repository owns the file
/// format; callers only see sessions. Every operation is asynchronous so file
/// access never blocks the UI thread.
/// </summary>
public interface ITrackerRepository
{
    /// <summary>Full path of the JSON file the entries are stored in.</summary>
    string FilePath { get; }

    /// <summary>Reads all entries.</summary>
    Task<IReadOnlyList<TrackerEntry>> GetAllAsync();

    /// <summary>Appends one entry, preserving every existing one.</summary>
    Task AddAsync(TrackerEntry entry);

    /// <summary>Writes the given entries, replacing the stored list (used by edits and deletes).</summary>
    Task SaveAsync(IReadOnlyList<TrackerEntry> entries);
}
