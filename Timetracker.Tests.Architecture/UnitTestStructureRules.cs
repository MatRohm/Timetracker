using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Rules for the unit-test projects:
/// <list type="bullet">
/// <item>a class that contains tests must carry <c>[TestFixture]</c> and be named
/// <c>&lt;ClassTested&gt;Tests</c>, where <c>&lt;ClassTested&gt;</c> is a production
/// class or interface (a production interface keeps its member surface, so a
/// fixture may also be named after it with the conventional <c>I</c> prefix
/// dropped, e.g. <c>ActivityMonitorInstallerTests</c> covers
/// <c>IActivityMonitorInstaller</c>);</item>
/// <item>a test method must be named
/// <c>&lt;MethodTested&gt;_When&lt;StateCondition&gt;_Should&lt;ExpectedResult&gt;</c>,
/// where <c>&lt;MethodTested&gt;</c> is a method of that same production type.</item>
/// </list>
/// Helpers and test doubles hold no tests and are therefore ignored.
/// </summary>
public sealed class UnitTestStructureRules
{
    private static readonly string[] TestAttributes =
        ["TestAttribute", "TestCaseAttribute", "AvaloniaTestAttribute"];

    private static readonly Regex MethodNamePattern = new(
        @"^(?<method>\w+)_When\w+_Should\w+$",
        RegexOptions.CultureInvariant);

    [Test]
    public void Unit_test_classes_are_named_after_the_class_under_test_and_carry_the_fixture_attribute()
    {
        var offenders = new List<string>();
        foreach (var fixture in UnitTestFixtures())
        {
            var name = fixture.Name;
            if (!name.EndsWith("Tests"))
            {
                offenders.Add($"  {name}: name must be <ClassTested>Tests");
                continue;
            }

            var tested = name[..^"Tests".Length];
            if (FindProductionType(ProductionTypes(), tested) is null)
            {
                offenders.Add($"  {name}: '{tested}' is not a production class or interface");
            }

            if (!HasAttribute(fixture, "TestFixtureAttribute"))
            {
                offenders.Add($"  {name}: missing [TestFixture]");
            }
        }

        offenders.Should().BeEmpty("unit-test classes must be named "
            + "<ClassTested>Tests and carry [TestFixture], but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void Unit_tests_are_named_after_the_method_under_test()
    {
        var productionTypes = ProductionTypes();
        var offenders = new List<string>();

        foreach (var fixture in UnitTestFixtures())
        {
            var name = fixture.Name;
            if (!name.EndsWith("Tests"))
            {
                continue; // reported by the class-name rule
            }

            var tested = name[..^"Tests".Length];
            var productionType = FindProductionType(productionTypes, tested);
            if (productionType is null)
            {
                continue; // reported by the class-name rule
            }

            var methods = MembersOf(productionType);
            foreach (var method in TestMethods(fixture))
            {
                var methodName = method.Split('(')[0];
                var match = MethodNamePattern.Match(methodName);
                if (!match.Success)
                {
                    offenders.Add($"  {name}.{methodName}: name must be "
                        + "<MethodTested>_When<StateCondition>_Should<ExpectedResult>");
                    continue;
                }

                var methodTested = match.Groups["method"].Value;
                if (!methods.Contains(methodTested))
                {
                    offenders.Add($"  {name}.{methodName}: '{methodTested}' is not a member of {tested}");
                }
            }
        }

        offenders.Should().BeEmpty("unit tests must be named after a method of the class under test, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Unit-test classes that contain at least one test method.</summary>
    private static IEnumerable<IType> UnitTestFixtures() =>
        SolutionArchitecture.Instance.Types
            .Where(t => t.Assembly.Name.EndsWith(".Tests.Unit"))
            .Where(t => TestMethods(t).Any());

    /// <summary>
    /// Production classes and interfaces by simple name; test assemblies are excluded.
    /// </summary>
    private static IReadOnlyDictionary<string, IType> ProductionTypes() =>
        SolutionArchitecture.Instance.Types
            .Where(t => !t.Assembly.Name.Contains(".Tests."))
            .Where(t => !t.IsNested)
            .Where(t => t is Class or Interface)
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>
    /// Resolves the production type a fixture name refers to: its exact name, or a
    /// production interface with the conventional <c>I</c> prefix dropped.
    /// </summary>
    private static IType? FindProductionType(
        IReadOnlyDictionary<string, IType> productionTypes, string tested)
    {
        if (productionTypes.TryGetValue(tested, out var exact))
        {
            return exact;
        }

        var interfaceName = "I" + tested;
        return productionTypes.TryGetValue(interfaceName, out var asInterface)
            ? asInterface
            : null;
    }

    /// <summary>
    /// The names a test may prefix with, i.e. the type's methods and properties
    /// (a property's getter is written as its plain name, e.g. "TaskName").
    /// </summary>
    private static HashSet<string> MembersOf(IType type) =>
        [.. type.Members
            .OfType<MethodMember>()
            .Select(m => m.Name.Split('(')[0])
            .Select(PlainName)
            .Where(n => n.Length > 0 && !n.StartsWith('.'))];

    private static string PlainName(string memberName) =>
        memberName.StartsWith("get_", StringComparison.Ordinal)
            ? memberName["get_".Length..]
            : memberName;

    private static IEnumerable<string> TestMethods(IType type) =>
        type.Members
            .OfType<MethodMember>()
            .Where(m => m.AttributeInstances.Any(a => TestAttributes.Contains(a.Type.Name)))
            .Select(m => m.Name);

    private static bool HasAttribute(IType type, string attributeName) =>
        type.AttributeInstances.Any(a => a.Type.Name == attributeName);
}
