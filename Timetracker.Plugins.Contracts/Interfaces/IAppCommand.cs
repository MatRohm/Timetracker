namespace Timetracker.Plugins.Contracts.Interfaces;

/// <summary>Runs once when the application starts or closes.</summary>
public interface IAppCommand
{
    /// <summary>Called after the composition root built the container.</summary>
    void OnAppStarted();

    /// <summary>Called when the main window is closing; the app exits afterwards.</summary>
    void OnAppClosing();
}
