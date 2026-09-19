using System.Windows.Input;

namespace Timetracker.Views;

/// <summary>
/// Wires a WinForms <see cref="Button"/> to an <see cref="ICommand"/>:
/// Click → Execute, and CanExecuteChanged → Enabled.
/// </summary>
internal static class CommandBindings
{
    public static void Bind(Button button, ICommand command)
    {
        button.Click += (_, _) => command.Execute(null);

        command.CanExecuteChanged += (_, _) => button.Enabled = command.CanExecute(null);
        button.Enabled = command.CanExecute(null);
    }
}