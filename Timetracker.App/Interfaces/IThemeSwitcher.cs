namespace Timetracker.App.Interfaces;

/// <summary>
/// Applies the user's appearance setting to the running application. The options
/// view calls it after a successful save and the composition root calls it at startup.
/// </summary>
public interface IThemeSwitcher
{
    /// <summary>
    /// Switches to the given theme value (see <c>General.Theme</c>); an unknown or
    /// empty value selects the light theme. Must be called on the UI thread.
    /// </summary>
    void Apply(string? theme);
}
