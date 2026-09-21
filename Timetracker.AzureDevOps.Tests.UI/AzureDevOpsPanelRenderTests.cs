using AwesomeAssertions;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Timetracker.Plugins;

namespace Timetracker.AzureDevOps.Tests.UI;

/// <summary>
/// UI smoke tests for the Azure DevOps add-in panel: build the real control on a
/// headless Avalonia instance and assert its embedded resources and button.
/// </summary>
[TestFixture]
public sealed class AzureDevOpsPanelRenderTests
{
    [AvaloniaTest]
    public void Panel_loads_its_embedded_icon_and_button()
    {
        var service = new AzureDevOpsService(configFilePath: "/nonexistent");
        var panel = new AzureDevOpsPanel(service, new FakeTrackerHost());

        panel.ImportButton.Should().NotBeNull();
        var icon = AzureDevOpsPanel.LoadBitmap(
            "Timetracker.AzureDevOps.azure-favicon.png", 16);
        icon.Should().NotBeNull("the favicon is embedded as a PNG resource");
    }

    private sealed class FakeTrackerHost : ITrackerUiHost
    {
        public void SetTaskName(string taskName) { }

        public void SetBookingElement(string bookingElement) { }

        public void ShowStatus(string message, TrackerStatusKind kind) { }
    }
}
