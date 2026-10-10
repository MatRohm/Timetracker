using Avalonia;
using Avalonia.Headless;
using Timetracker.Tests.UI;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Timetracker.Tests.UI;

/// <summary>
/// Headless Avalonia application used by all UI tests: no windowing system is
/// required, so the real views can be constructed and asserted on any platform.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
