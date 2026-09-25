using Timetracker.Interfaces;
using Avalonia.Threading;

namespace Timetracker.Views;

/// <summary>Avalonia implementation of <see cref="IUiTimer"/>; ticks on the UI thread.</summary>
internal sealed class AvaloniaUiTimer : IUiTimer
{
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500),
    };

    public event Action? Tick;

    public AvaloniaUiTimer()
    {
        _timer.Tick += (_, _) => Tick?.Invoke();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Stop();
}
