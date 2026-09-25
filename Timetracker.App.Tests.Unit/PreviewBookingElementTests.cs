using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.ViewModels;

namespace Timetracker.Tests.Unit;

/// <summary>
/// Covers the tracker view model's preview-field behavior. The Azure DevOps
/// add-in fills these fields; the tracker must consume them for exactly one
/// session, so the test lives with the tracker rather than the add-in.
/// </summary>
public sealed class PreviewBookingElementTests
{
    [Test]
    public void TrackerViewModel_WhenPreviewBookingElementIsSet_ShouldUseItForTheNextSessionThenClearIt()
    {
        var (repo, _) = RepositoryFake.Create();
        using var vm = new TrackerViewModel(repo, new FakeTimer(), new FakeIdleTimeProvider());
        vm.PreviewBookingElement = "Quarterly figures";

        vm.TaskName = "Report";
        vm.StartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        repo.GetAll().Single().BookingElement.Should().Be("Quarterly figures");
        vm.PreviewBookingElement.Should().BeEmpty("the preview is consumed by one session");
    }
}
