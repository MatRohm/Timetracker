using Avalonia;
using Timetracker.App.Services;

namespace Timetracker.Tests.UI;

/// <summary>
/// Minimal application that loads the same themes as the real app: the app's
/// Dracula/Alucard Fluent palettes plus the DataGrid theme, so grids render in the
/// app's own tabs exactly as they do at runtime.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(ThemeSwitcher.CreateTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(
            new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
    }
}
