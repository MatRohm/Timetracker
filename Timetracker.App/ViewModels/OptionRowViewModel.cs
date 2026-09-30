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
