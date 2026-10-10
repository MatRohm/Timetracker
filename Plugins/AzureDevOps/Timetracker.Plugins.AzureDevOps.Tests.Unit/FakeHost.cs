using Timetracker.Plugins.Contracts;
using Timetracker.Plugins.Contracts.Interfaces;

namespace Timetracker.Plugins.AzureDevOps.Tests.Unit;

internal sealed class FakeHost : ITrackerUiCommand
{
    public string TaskName { get; private set; } = "";

    public string BookingElement { get; private set; } = "";

    public string? LastStatusMessage { get; private set; }

    public TrackerStatusKind? LastStatusKind { get; private set; }

    public void SetTaskName(string taskName) => TaskName = taskName;

    public void SetBookingElement(string bookingElement) => BookingElement = bookingElement;

    public void ShowStatus(string message, TrackerStatusKind kind)
    {
        LastStatusMessage = message;
        LastStatusKind = kind;
    }
}
