using Timetracker.Models;

namespace Timetracker.Interfaces;

/// <summary>
/// Reads and writes the tracker entries. The concrete repository owns the file
/// format; callers only see sessions.
/// </summary>
public interface ITrackerRepository
{
    /// <summary>Full path of the JSON file the entries are stored in.</summary>
    string FilePath { get; }

    /// <summary>Reads all entries.</summary>
    IReadOnlyList<TrackerEntry> GetAll();

    /// <summary>Appends one entry, preserving every existing one.</summary>
    void Add(TrackerEntry entry);

    /// <summary>Writes the given entries, replacing the stored list (used by edits and deletes).</summary>
    void Save(IReadOnlyList<TrackerEntry> entries);
}
