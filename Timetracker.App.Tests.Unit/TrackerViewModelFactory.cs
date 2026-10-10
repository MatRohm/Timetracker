using Microsoft.Extensions.Logging.Abstractions;
using Timetracker.App.Interfaces;
using Timetracker.App.Services;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Tests.Unit;

/// <summary>
/// Creates the tracker view model wired to the fakes. Tests that need to drive the
/// timer keep a reference to it and pass it in; the others accept fresh instances.
/// </summary>
public static class TrackerViewModelFactory
{
    public static TrackerViewModel Create(
        ITrackerRepository repository,
        FakeTimer? timer = null,
        Func<DateTimeOffset>? now = null)
    {
        var result = new TrackerViewModel(
            repository,
            timer ?? new FakeTimer(),
            new EntryEditor(repository.SaveAsync, NullLogger<EntryEditor>.Instance),
            NullLogger<TrackerViewModel>.Instance,
            now);
        return result;
    }
}
