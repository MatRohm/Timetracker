using Avalonia;
using Avalonia.Themes.Fluent;

namespace Timetracker.Tests.UI;

/// <summary>
/// Minimal application that loads the same themes as the real app: the Fluent
/// theme plus the DataGrid theme, so grids render in the app's own tabs.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(
            new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
    }
}
