namespace Timetracker.ViewModels;

/// <summary>UI-agnostic timer abstraction so the view model does not depend on WinForms.</summary>
public interface IUiTimer : IDisposable
{
    event Action? Tick;

    void Start();

    void Stop();
}