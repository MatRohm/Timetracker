using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Timetracker.AzureDevOps.Tests.UI;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Timetracker.AzureDevOps.Tests.UI;

/// <summary>
/// Headless Avalonia application used by the Azure DevOps UI tests: no windowing
/// system is required, so the panel can be constructed and asserted on any
/// platform.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>Minimal application that loads the same theme as the real app.</summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
