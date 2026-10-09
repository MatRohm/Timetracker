using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Guards the input of every other architecture rule: all solution assemblies are
/// listed and present on disk. Without this, a missing or partial build shows up as
/// a misleading rule failure (e.g. "X is not a member of Y") instead of as what it is.
/// </summary>
public sealed class AnalysedAssemblyRules
{
    private static readonly string[] ExpectedAssemblies =
    [
        "Timetracker.dll",
        "Timetracker.Plugins.Contracts.dll",
        "Timetracker.Plugins.AzureDevOps.dll",
        "Timetracker.Plugins.ActivityMonitor.dll",
        "Timetracker.Plugins.ActivityMonitor.App.dll",
        "Timetracker.Plugins.WeekView.dll",
        "Timetracker.App.Tests.Unit.dll",
        "Timetracker.Plugins.AzureDevOps.Tests.Unit.dll",
        "Timetracker.Plugins.ActivityMonitor.Tests.Unit.dll",
        "Timetracker.Plugins.WeekView.Tests.Unit.dll",
        "Timetracker.Tests.UI.dll",
    ];

    [Test]
    public void Every_solution_assembly_is_listed_for_analysis()
    {
        var listed = SolutionArchitecture.AssemblyPaths().Select(Path.GetFileName).ToList();

        listed.Should().Contain(ExpectedAssemblies,
            $"{SolutionArchitecture.ListFileName} must list every project the architecture rules analyse");
    }

    [Test]
    public void Every_listed_assembly_exists_in_its_project_output()
    {
        var missing = SolutionArchitecture.AssemblyPaths().Where(path => !File.Exists(path)).ToList();

        missing.Should().BeEmpty("every listed assembly must have been built, but these are missing: "
            + string.Join(", ", missing));
    }

    [Test]
    public void Analysed_assemblies_are_not_copies_next_to_the_test_host()
    {
        // The rules must read each project's own output, never a copy that can lag behind.
        var hostDirectory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var copies = SolutionArchitecture.AssemblyPaths()
            .Where(path => string.Equals(
                Path.GetDirectoryName(Path.GetFullPath(path)), hostDirectory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        copies.Should().BeEmpty("analysed assemblies must come from their project output folders, but found: "
            + string.Join(", ", copies));
    }
}
