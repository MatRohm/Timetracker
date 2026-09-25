namespace Timetracker.Tests.Architecture;

/// <summary>Locates the solution's .csproj files from the running test host.</summary>
internal static class SolutionFiles
{
    /// <summary>Every project file of the solution, in a stable order.</summary>
    public static IReadOnlyList<string> ProjectFiles() =>
        [.. Directory
            .EnumerateFiles(RepositoryRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(path => path)];

    /// <summary>
    /// The repository root, found by walking up from the test host until the
    /// solution file is reached.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root (no .slnx found).");
    }
}
