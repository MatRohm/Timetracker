using System.Windows.Input;

namespace Timetracker.Plugins.Contracts.ViewModels.Mvvm;

/// <summary>
/// Async variant of <see cref="RelayCommand"/>: runs a <see cref="Func{TResult}"/>
/// returning a <see cref="Task"/>. Exceptions are the caller's responsibility —
/// the view model methods this command wraps catch and report their own errors,
/// so the awaited task is not expected to throw.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
