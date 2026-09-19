namespace Timetracker.ViewModels;

/// <summary>One autocomplete suggestion: a known task name and its total time.</summary>
public sealed class SuggestionItem
{
    public SuggestionItem(string name, double totalSeconds)
    {
        Name = name;
        TotalSeconds = totalSeconds;
    }

    public string Name { get; }

    public double TotalSeconds { get; }

    public string TotalText => TimeSpan.FromSeconds(TotalSeconds).ToString(@"hh\:mm\:ss");

    public string DisplayText => $"{Name}  ({TotalText})";
}