using System.Collections.ObjectModel;
using Timetracker.App.Models;

namespace Timetracker.App.ViewModels;

/// <summary>
/// Autocomplete suggestions for the task-name input: builds the list shown below
/// the field from the known task names and their accumulated time. Kept separate
/// from <see cref="TrackerViewModel"/> so the input concern can be tested on its
/// own; the tracker forwards its own <see cref="Suggestions"/> binding to this.
/// </summary>
public sealed class SuggestionListViewModel : ObservableObject
{
    /// <summary>Most suggestions shown for one query.</summary>
    private const int MaxSuggestions = 8;

    private readonly ObservableCollection<SuggestionItem> _suggestions = new();

    /// <summary>Autocomplete suggestions for the current task-name input.</summary>
    public ObservableCollection<SuggestionItem> Suggestions => _suggestions;

    /// <summary>True while the suggestion list should be shown below the task input.</summary>
    public bool ShowSuggestions => Suggestions.Count > 0;

    /// <summary>
    /// Rebuilds the suggestions for the given input against every saved session
    /// (not just the visible page). Matches are case-insensitive substrings, most
    /// used first, capped at eight; an exact match is never suggested — the user
    /// just presses Start instead. An empty query clears the list.
    /// </summary>
    public void Refresh(string typed, IReadOnlyList<TrackerEntry> sessions)
    {
        var query = typed.Trim();

        Suggestions.Clear();

        if (query.Length > 0)
        {
            var matches = sessions
                .GroupBy(e => e.Task.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new SuggestionItem(g.Key, g.Sum(e => e.DurationSeconds)))
                .Where(s => s.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                            && !s.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.TotalSeconds)
                .Take(MaxSuggestions);

            foreach (var suggestion in matches)
            {
                Suggestions.Add(suggestion);
            }
        }

        OnPropertyChanged(nameof(ShowSuggestions));
    }
}
