using FakeItEasy;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;

namespace Timetracker.App.Tests.Unit;

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
        A.CallTo(() => repo.GetAllAsync(A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult((IReadOnlyList<TrackerEntry>)list.ToList()));
        A.CallTo(() => repo.AddAsync(A<TrackerEntry>._, A<CancellationToken>._))
            .Invokes((TrackerEntry e, CancellationToken _) => list.Add(e));
        A.CallTo(() => repo.SaveAsync(A<IReadOnlyList<TrackerEntry>>._, A<CancellationToken>._))
            .Invokes((IReadOnlyList<TrackerEntry> entries, CancellationToken _) =>
            {
                list.Clear();
                list.AddRange(entries);
            });
        return (repo, path);
    }

    public static IReadOnlyList<TrackerEntry> Persisted(ITrackerRepository repo) =>
        repo.GetAllAsync().GetAwaiter().GetResult();
}
