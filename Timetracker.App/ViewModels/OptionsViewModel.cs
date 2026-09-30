using System.ComponentModel;
using Timetracker.App.Interfaces;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.App.ViewModels;

/// <summary>A heading and its options in the options view.</summary>
public sealed record OptionSection(string Title, IReadOnlyList<OptionRowViewModel> Rows);

/// <summary>
/// State of the options tab: one section per <see cref="IOptionDefinitionQuery"/> in
/// registration order (the app's "General" section first), and saving the edited
/// values through the <see cref="IOptionCommand"/>.
/// </summary>
public sealed class OptionsViewModel : ObservableObject
{
    private readonly IOptionQuery _query;
    private readonly IOptionCommand _command;
    private string _statusText = "";

    public OptionsViewModel(
        IOptionQuery query, IOptionCommand command, IEnumerable<IOptionDefinitionQuery> contributors, IFileExplorer explorer)
    {
        _query = query;
        _command = command;
        Sections =
        [
            .. contributors.Select(contributor => new OptionSection(
                contributor.Section,
                [.. contributor.Options.Select(option =>
                    new OptionRowViewModel(option, query.GetValue(option.Key), explorer))])),
        ];

        SaveCommand = new AsyncRelayCommand(() => SaveAsync(), () => HasChanges);
        foreach (var row in Sections.SelectMany(section => section.Rows))
        {
            row.PropertyChanged += OnRowChanged;
        }
    }

    public IReadOnlyList<OptionSection> Sections { get; }

    /// <summary>True when any option was edited since it was loaded or saved.</summary>
    public bool HasChanges => Sections.SelectMany(section => section.Rows).Any(row => row.IsChanged);

    /// <summary>Result of the last save, shown below the options.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public AsyncRelayCommand SaveCommand { get; }

    /// <summary>Stores every edited option; a failed write is reported in <see cref="StatusText"/>.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var changed = Sections.SelectMany(section => section.Rows).Where(row => row.IsChanged).ToList();
        try
        {
            foreach (var row in changed)
            {
                await _command.SetValueAsync(row.Definition.Key, row.Value, cancellationToken);
                row.AcceptValue();
            }
            StatusText = "Options saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = "Could not save the options: " + ex.Message;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OptionRowViewModel.IsChanged))
        {
            return;
        }
        OnPropertyChanged(nameof(HasChanges));
        SaveCommand.RaiseCanExecuteChanged();
    }
}
