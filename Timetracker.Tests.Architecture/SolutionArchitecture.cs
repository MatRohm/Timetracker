using ArchUnitNET.Loader;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Loads the solution's assemblies once for every architecture rule. The other
/// projects are built (not referenced) by this project, which lists their actual
/// output assemblies in <see cref="ListFileName"/>; each is analysed in its own
/// project's output folder, so a production assembly and its tests always come
/// from the same build.
/// </summary>
internal static class SolutionArchitecture
{
    /// <summary>Written next to the test host by the WriteAnalysedAssemblyList target.</summary>
    public const string ListFileName = "analysed-assemblies.txt";

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Lazy = new(Build);

    public static ArchUnitNET.Domain.Architecture Instance => Lazy.Value;

    /// <summary>
    /// The analysed assembly paths as listed by the build. Throws when the list is
    /// missing, i.e. the architecture project was not built.
    /// </summary>
    public static IReadOnlyList<string> AssemblyPaths()
    {
        var listPath = Path.Combine(AppContext.BaseDirectory, ListFileName);
        if (!File.Exists(listPath))
        {
            throw new InvalidOperationException(
                $"{ListFileName} is missing next to the test host; build Timetracker.Tests.Architecture "
                + "so it can list the assemblies it analyses.");
        }

        var result = File.ReadAllLines(listPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
        return result;
    }

    private static ArchUnitNET.Domain.Architecture Build()
    {
        var paths = AssemblyPaths();
        var missing = paths.Where(path => !File.Exists(path)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Listed assemblies are missing; rebuild the solution: " + string.Join(", ", missing));
        }

        var loader = new ArchLoader();
        foreach (var path in paths)
        {
            loader = loader.LoadFilteredDirectory(
                Path.GetDirectoryName(path)!, Path.GetFileName(path), SearchOption.TopDirectoryOnly);
        }

        var result = loader.Build();
        return result;
    }
}
