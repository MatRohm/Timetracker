using System.Xml.Linq;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Project-reference rules, checked against the .csproj files themselves (a
/// reference is a project-file fact, which compiled-assembly analysis cannot see).
/// </summary>
public sealed class ProjectReferenceRules
{
    /// <summary>The application project every other non-test project must not reference.</summary>
    private const string AppProject = "Timetracker.App";

    /// <summary>The only product project a non-App project may reference.</summary>
    private const string PluginsProject = "Timetracker.Plugins";

    /// <summary>The architecture project itself, which builds every project to analyse it.</summary>
    private const string ArchitectureProject = "Timetracker.Tests.Architecture";

    [Test]
    public void Only_unit_test_projects_reference_the_app_project()
    {
        var offenders = Projects()
            .Where(p => p.References(AppProject))
            .Where(p => p.Name != ArchitectureProject)
            .Where(p => !IsUnitTest(p.Name) && !IsUiTest(p.Name))
            .Select(p => p.Name);

        offenders.Should().BeEmpty(
            $"only unit tests may reference {AppProject} (UI tests are exempt), but found: " +
            string.Join(", ", offenders));
    }

    [Test]
    public void Product_projects_only_reference_the_plugins_project()
    {
        var offenders = Projects()
            .Where(p => !IsApp(p.Name) && !IsTest(p.Name))
            .SelectMany(p => p.ProjectReferences
                .Where(r => r != PluginsProject)
                .Select(r => $"{p.Name} -> {r}"));

        offenders.Should().BeEmpty(
            $"a product project may only reference {PluginsProject}, but found: " +
            string.Join(", ", offenders));
    }

    private static bool IsApp(string name) => name == AppProject;

    private static bool IsTest(string name) => name.Contains(".Tests.");

    private static bool IsUnitTest(string name) => name.EndsWith(".Tests.Unit");

    private static bool IsUiTest(string name) => name.EndsWith(".Tests.UI");

    private static IReadOnlyList<ProjectFile> Projects() =>
        [.. SolutionFiles.ProjectFiles().Select(ProjectFile.Read)];

    /// <summary>A .csproj and the project names it references.</summary>
    private sealed class ProjectFile
    {
        private ProjectFile(string name, IReadOnlyList<string> projectReferences)
        {
            Name = name;
            ProjectReferences = projectReferences;
        }

        public string Name { get; }

        public IReadOnlyList<string> ProjectReferences { get; }

        public bool References(string projectName) => ProjectReferences.Contains(projectName);

        public static ProjectFile Read(string path)
        {
            var document = XDocument.Load(path);
            var references = document.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(include => include is not null)
                // .csproj paths use Windows separators; normalise so the name is
                // extracted correctly on any OS.
                .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
                .ToList();
            return new ProjectFile(Path.GetFileNameWithoutExtension(path), references);
        }
    }
}
