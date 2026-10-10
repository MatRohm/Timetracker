namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

/// <summary>Shared data and test doubles for the Azure DevOps unit tests.</summary>
internal static class AzureDevOpsTestHelpers
{
    internal static AzureDevOpsConfig Config() => new()
    {
        Url = "https://dev.azure.com/my-org",
        Project = "MyProject",
        Pat = "secret-token",
        BookingElementField = "Custom.BookingElement",
    };
}
