using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>WinForms implementation of <see cref="IUiTimer"/>; ticks on the UI thread.</summary>
internal sealed class FormsUiTimer : IUiTimer
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };

    public event Action? Tick;

    public FormsUiTimer()
    {
        _timer.Tick += (_, _) => Tick?.Invoke();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Dispose();
}
