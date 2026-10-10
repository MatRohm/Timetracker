namespace Timetracker.Plugins.AzureDevOps;

/// <summary>Result of a work-item lookup.</summary>
public sealed record WorkItemInfo(int Id, string Title, string BookingElement)
{
    public bool IsEmpty => Id == 0;
}
