using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;
using Timetracker.App.Services;
using Timetracker.App.ViewModels;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Tests.Unit.Services;

/// <summary>
/// The session host exposes the tracker's log to add-ins: reading sessions, booking
/// a gap and applying planned changes, each followed by a change notification.
/// </summary>
[TestFixture]
public sealed class TrackedSessionsHostTests
{
    [Test]
    public async Task Sessions_WhenTheTrackerHasEntries_ShouldExposeThemAsTrackedSessions()
    {
        var (host, _, tracker) = Create(Entry("Report", 9));
        await tracker.InitialLoad;

        host.Sessions.Should().ContainSingle().Which.Task.Should().Be("Report");
    }

    [Test]
    public async Task BookAsync_WhenCalled_ShouldAppendASession()
    {
        var (host, repo, tracker) = Create();
        await tracker.InitialLoad;
        var range = new TimeRange(
            new DateTimeOffset(2026, 9, 21, 10, 15, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 21, 11, 5, 0, TimeSpan.FromHours(2)));

        var booked = await host.BookAsync(range, "Report", "Project X");

        booked.Should().BeTrue();
        RepositoryFake.Persisted(repo).Single().Task.Should().Be("Report");
    }

    [Test]
    public async Task BookAsync_WhenTheBookingElementIsEmpty_ShouldInheritTheTasksLatestBookingElement()
    {
        var (host, repo, tracker) = Create(Entry("Report", 9, "Project X"));
        await tracker.InitialLoad;
        var range = new TimeRange(
            new DateTimeOffset(2026, 9, 21, 10, 15, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 21, 11, 5, 0, TimeSpan.FromHours(2)));

        var booked = await host.BookAsync(range, "Report", "");

        booked.Should().BeTrue();
        RepositoryFake.Persisted(repo).Last().BookingElement.Should().Be("Project X");
    }

    [Test]
    public async Task ApplyChangesAsync_WhenAChangeIsApplied_ShouldPersistIt()
    {
        var (host, repo, tracker) = Create(Entry("Report", 9));
        await tracker.InitialLoad;
        var session = host.Sessions.Single();
        var updated = session.Reschedule(session.Start, session.End.AddMinutes(30));

        var saved = await host.ApplyChangesAsync([new SessionChange(session, updated)]);

        saved.Should().BeTrue();
        RepositoryFake.Persisted(repo).Single().DurationSeconds.Should().Be(3600);
    }

    [Test]
    public async Task ApplyChangesAsync_WhenTheOriginalIsNoLongerInTheLog_ShouldReturnFalse()
    {
        var (host, repo, tracker) = Create(Entry("Report", 9));
        await tracker.InitialLoad;
        var stale = new TrackedSession
        {
            Id = Guid.NewGuid(),
            Task = "Report",
            Start = new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.FromHours(2)),
            End = new DateTimeOffset(2026, 9, 19, 9, 30, 0, TimeSpan.FromHours(2)),
        };

        var saved = await host.ApplyChangesAsync([new SessionChange(stale, stale)]);

        saved.Should().BeFalse();
        RepositoryFake.Persisted(repo).Should().ContainSingle("nothing was applied");
    }

    [Test]
    public async Task Sessions_WhenRefreshed_ShouldRaiseChanged()
    {
        var (host, _, tracker) = Create();
        await tracker.InitialLoad;
        var raised = 0;
        host.Changed += (_, _) => raised++;

        await tracker.RefreshEntriesAsync();

        raised.Should().Be(1);
    }

    [Test]
    public void RunningSince_WhenASessionRuns_ShouldExposeItsStart()
    {
        var (host, _, tracker) = Create();
        tracker.TaskName = "Report";
        tracker.StartCommand.Execute(null);

        host.RunningSince.Should().NotBeNull();
    }

    private static (TrackedSessionsHost Host, ITrackerRepository Repo, TrackerViewModel Tracker) Create(
        params TrackerEntry[] seed)
    {
        var (repo, _) = RepositoryFake.Create(seed);
        var editor = new EntryEditor(repo.SaveAsync, NullLogger<EntryEditor>.Instance);
        var tracker = new TrackerViewModel(repo, new FakeTimer(), editor, NullLogger<TrackerViewModel>.Instance);
        var host = new TrackedSessionsHost(repo, editor, tracker, NullLogger<TrackedSessionsHost>.Instance);
        return (host, repo, tracker);
    }

    private static TrackerEntry Entry(string task, int hour, string bookingElement = "") => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(2026, 9, 19, hour, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 19, hour, 30, 0, TimeSpan.FromHours(2)),
        Duration = "00:30:00",
        DurationSeconds = 1800,
    };
}
