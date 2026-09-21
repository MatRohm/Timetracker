using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Timetracker.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Timetracker.Tests;

/// <summary>
/// Headless Avalonia application used by the UI tests: no windowing system is
/// required, so the views can be constructed and asserted on any platform.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>Minimal application that loads the same themes as the real app.</summary>
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
