namespace Timetracker.Plugins.Contracts.ViewModels;

/// <summary>One autocomplete suggestion: a known task name and its total time.</summary>
public sealed class SuggestionItem(string name, double totalSeconds)
{
    public string Name { get; } = name;

    public double TotalSeconds { get; } = totalSeconds;

    public string TotalText => TimeSpan.FromSeconds(TotalSeconds).ToString(@"hh\:mm\:ss");

    public string DisplayText => $"{Name}  ({TotalText})";
}
