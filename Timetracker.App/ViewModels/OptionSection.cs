namespace Timetracker.App.ViewModels;

/// <summary>A heading and its options in the options view.</summary>
public sealed record OptionSection(string Title, IReadOnlyList<OptionRowViewModel> Rows);
