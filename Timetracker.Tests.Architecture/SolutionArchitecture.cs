using ArchUnitNET.Loader;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Loads the solution's assemblies once for every architecture rule. The other
/// projects are built (not referenced) by this project, so their assemblies sit
/// next to the architecture test host and are analysed from there.
/// </summary>
internal static class SolutionArchitecture
{
    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Lazy = new(Build);

    public static ArchUnitNET.Domain.Architecture Instance => Lazy.Value;

    private static ArchUnitNET.Domain.Architecture Build() =>
        new ArchLoader()
            .LoadFilteredDirectory(
                AppContext.BaseDirectory,
                "Timetracker*.dll",
                SearchOption.TopDirectoryOnly)
            .Build();
}
