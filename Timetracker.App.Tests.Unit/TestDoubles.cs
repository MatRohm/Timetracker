using Timetracker.ActivityMonitor.Interfaces;
using Timetracker.Interfaces;
using FakeItEasy;
using Timetracker.Models;

namespace Timetracker.Tests.Unit;

/// <summary>
/// Manual fake for the UI timer abstraction: <see cref="IUiTimer.Tick"/> is an
/// event with add/remove accessors, which FakeItEasy cannot configure inline
/// (expression trees may not contain assignments). The repository fakes below
/// use FakeItEasy.
/// </summary>
public sealed class FakeTimer : IUiTimer
{
    private Action? _tick;

    public event Action? Tick
    {
        add => _tick += value;
        remove => _tick -= value;
    }

    public void RaiseTick() => _tick?.Invoke();

    public void Start() { }

    public void Stop() { }

    public void Dispose() { }
}

/// <summary>
/// Idle provider for tests: the idle duration is set directly, so no real
/// platform API is touched.
/// </summary>
public sealed class FakeIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan CurrentIdleTime { get; set; }
}

/// <summary>FakeItEasy fake wrapping an in-memory list as repository.</summary>
public static class RepositoryFake
{
    public static (ITrackerRepository Repo, string Path) Create(params TrackerEntry[] seed)
    {
        var path = Path.Combine(Path.GetTempPath(), "opencode", "tt-unit",
            $"tt-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var list = seed.ToList();
        var repo = A.Fake<ITrackerRepository>();
        A.CallTo(() => repo.FilePath).Returns(path);
        A.CallTo(() => repo.GetAll()).ReturnsLazily(() => list.ToList());
        A.CallTo(() => repo.Add(A<TrackerEntry>._))
            .Invokes((TrackerEntry e) => list.Add(e));
        A.CallTo(() => repo.Save(A<IReadOnlyList<TrackerEntry>>._))
            .Invokes((IReadOnlyList<TrackerEntry> entries) =>
            {
                list.Clear();
                list.AddRange(entries);
            });
        return (repo, path);
    }

    public static IReadOnlyList<TrackerEntry> Persisted(ITrackerRepository repo) => repo.GetAll();
}
