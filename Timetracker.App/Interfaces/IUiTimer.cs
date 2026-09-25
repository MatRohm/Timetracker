namespace Timetracker.Interfaces;

/// <summary>UI-agnostic timer abstraction so the view model does not depend on a UI framework.</summary>
public interface IUiTimer : IDisposable
{
    event Action? Tick;

    void Start();

    void Stop();
}
