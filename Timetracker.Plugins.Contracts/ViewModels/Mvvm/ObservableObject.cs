using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Timetracker.Plugins.Contracts.ViewModels.Mvvm;

/// <summary>
/// Base for view models: <see cref="INotifyPropertyChanged"/> plus a typed
/// change-tracking setter. Shared by the app and every add-in's view models.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
