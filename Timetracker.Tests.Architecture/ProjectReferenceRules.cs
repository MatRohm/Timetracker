using System.Xml.Linq;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Project-reference rules, checked against the .csproj files themselves (a
/// reference is a project-file fact, which compiled-assembly analysis cannot see).
///
/// Test projects are exempt: <c>&lt;ProjectName&gt;.Tests.Unit</c> and
/// <c>&lt;ProjectName&gt;.Tests.UI</c> may reference any assembly they need to
/// arrange their tests (the app, an add-in, or a shared test-double project),
/// because tests are allowed to depend on everything they verify.
/// </summary>
public sealed class ProjectReferenceRules
{
    /// <summary>The application project every other non-test project must not reference.</summary>
    private const string AppProject = "Timetracker.App";

    /// <summary>The only product project a non-App product project may reference.</summary>
    private const string PluginsProject = "Timetracker.Plugins";

    /// <summary>The architecture project itself, which builds every project to analyse it.</summary>
    private const string ArchitectureProject = "Timetracker.Tests.Architecture";

    /// <summary>Product projects: everything that is neither the app nor a test project.</summary>
    private static IEnumerable<ProjectFile> ProductProjects() =>
        Projects().Where(p => !IsApp(p.Name) && !IsTest(p.Name));

    [Test]
    public void Only_non_test_projects_are_subject_to_the_app_reference_rule()
    {
        // Test projects (unit and UI) are exempt and may reference the app; the
        // architecture project also references it, only to build it for analysis.
        var offenders = ProductProjects()
            .Where(p => p.Name != ArchitectureProject)
            .Where(p => p.References(AppProject))
            .Select(p => p.Name);

        offenders.Should().BeEmpty(
            $"a product project must not reference {AppProject}, but found: "
            + string.Join(", ", offenders));
    }

    [Test]
    public void Product_projects_only_reference_the_plugins_project()
    {
        var offenders = ProductProjects()
            .SelectMany(p => p.ProjectReferences
                .Where(r => r != PluginsProject)
                .Select(r => $"{p.Name} -> {r}"));

        offenders.Should().BeEmpty(
            $"a product project may only reference {PluginsProject}, but found: "
            + string.Join(", ", offenders));
    }

    [Test]
    public void Test_projects_are_exempt_from_the_product_reference_rules()
    {
        // The merged UI project must be free to reference the app and the add-in it
        // renders; this pins that the exemption is recognised for test projects.
        var uiTest = Projects().Single(p => p.Name == "Timetracker.Tests.UI");
        var unitTest = Projects().Single(p => p.Name == "Timetracker.App.Tests.Unit");

        IsTest(uiTest.Name).Should().BeTrue("UI tests are a test project");
        IsTest(unitTest.Name).Should().BeTrue("unit tests are a test project");
        uiTest.References("Timetracker.AzureDevOps").Should().BeTrue(
            "the UI test project references the add-in it renders, which the rules must allow");
    }

    private static bool IsApp(string name) => name == AppProject;

    private static bool IsTest(string name) => name.Contains(".Tests.");

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
