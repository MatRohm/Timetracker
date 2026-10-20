using Timetracker.App.Interfaces;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.ViewModels;

/// <summary>
/// One option in the options view: its label, the edited value and, for paths, the
/// command that shows the path in the file explorer.
/// </summary>
public sealed class OptionRowViewModel : ObservableObject
{
    private readonly IFileExplorer _explorer;

    /// <summary>The value as it was loaded or last saved.</summary>
    private string _savedValue;

    private string _value;

    /// <param name="definition">The option as its component defined it.</param>
    /// <param name="storedValue">The value from the store; null when none was stored.</param>
    /// <param name="explorer">Shows path values in the file explorer.</param>
    public OptionRowViewModel(OptionDefinition definition, string? storedValue, IFileExplorer explorer)
    {
        Definition = definition;
        _explorer = explorer;
        _savedValue = definition.IsReadOnly ? definition.DefaultValue : storedValue ?? definition.DefaultValue;
        _value = _savedValue;
        ShowInExplorerCommand = new RelayCommand(ShowInExplorer, () => IsPath);
    }

    public OptionDefinition Definition { get; }

    public string Label => Definition.Label;

    public bool IsReadOnly => Definition.IsReadOnly;

    public bool IsPath => Definition.Kind == OptionKind.Path;

    public bool IsSecret => Definition.Kind == OptionKind.Secret;

    /// <summary>True when the option is edited with a dropdown of fixed choices.</summary>
    public bool IsChoice => Definition.Kind == OptionKind.Choice;

    /// <summary>The allowed choices; empty for other option kinds.</summary>
    public IReadOnlyList<OptionChoice> Choices => Definition.Choices ?? [];

    /// <summary>The labels of <see cref="Choices"/>, in the same order.</summary>
    public IReadOnlyList<string> ChoiceLabels => [.. Choices.Select(choice => choice.Label)];

    /// <summary>
    /// Index of the selected choice, or -1 when the stored value matches none.
    /// Setting a valid index stores that choice's value.
    /// </summary>
    public int SelectedChoiceIndex
    {
        get
        {
            for (var index = 0; index < Choices.Count; index++)
            {
                if (string.Equals(Choices[index].Value, Value, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }
        set
        {
            if (value >= 0 && value < Choices.Count)
            {
                Value = Choices[value].Value;
            }
        }
    }

    public string HintText => Definition.HintText;

    public bool HasHint => Definition.HintText.Length > 0;

    /// <summary>The edited value; read-only options ignore changes.</summary>
    public string Value
    {
        get => _value;
        set
        {
            if (IsReadOnly || !SetProperty(ref _value, value ?? ""))
            {
                return;
            }
            OnPropertyChanged(nameof(IsChanged));
            OnPropertyChanged(nameof(SelectedChoiceIndex));
        }
    }

    /// <summary>True when the value differs from the loaded or last saved one.</summary>
    public bool IsChanged => _value != _savedValue;

    public RelayCommand ShowInExplorerCommand { get; }

    /// <summary>Marks the current value as saved.</summary>
    public void AcceptValue()
    {
        _savedValue = _value;
        OnPropertyChanged(nameof(IsChanged));
    }

    private void ShowInExplorer() => _explorer.Show(_value);
}
