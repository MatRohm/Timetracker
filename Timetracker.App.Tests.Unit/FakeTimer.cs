using Timetracker.App.Interfaces;

namespace Timetracker.App.Tests.Unit;

/// <summary>
/// Manual fake for the UI timer abstraction: <see cref="IUiTimer.Tick"/> is an
/// event with add/remove accessors, which FakeItEasy cannot configure inline
/// (expression trees may not contain assignments). The repository fakes below
/// use FakeItEasy.
/// </summary>
public sealed class FakeTimer : IUiTimer
{
    private Action? _tick;

    public event Action? Tick
    {
        add => _tick += value;
        remove => _tick -= value;
    }

    public void RaiseTick() => _tick?.Invoke();

    public void Start() { }

    public void Stop() { }

    public void Dispose() { }
}
