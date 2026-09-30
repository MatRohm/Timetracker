using Timetracker.Plugins.Contracts.Interfaces;
using AwesomeAssertions;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Timetracker.Plugins.AzureDevOps;
using Timetracker.Plugins.AzureDevOps.Views;
using Timetracker.Plugins.Contracts;

namespace Timetracker.Tests.UI;

/// <summary>
/// UI smoke tests for the Azure DevOps add-in panel: build the real control on a
/// headless Avalonia instance and assert its embedded resources and button.
/// </summary>
[TestFixture]
public sealed class AzureDevOpsPanelRenderTests
{
    [AvaloniaTest]
    public void AzureDevOpsPanel_WhenLoaded_ShouldLoadEmbeddedIconAndButton()
    {
        var service = new AzureDevOpsService(new AzureDevOpsConfig());
        var panel = new AzureDevOpsPanel(service, new FakeTrackerHost());

        panel.ImportButton.Should().NotBeNull();
        var icon = AzureDevOpsPanel.LoadBitmap(
            "Timetracker.Plugins.AzureDevOps.azure-favicon.png", 16);
        icon.Should().NotBeNull("the favicon is embedded as a PNG resource");
    }

    private sealed class FakeTrackerHost : ITrackerUiCommand
    {
        public void SetTaskName(string taskName) { }

        public void SetBookingElement(string bookingElement) { }

        public void ShowStatus(string message, TrackerStatusKind kind) { }
    }
}
