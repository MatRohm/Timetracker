using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Test methods must read as "&lt;ComponentTested&gt;_When&lt;StateCondition&gt;_Should&lt;ExpectedResult&gt;".
/// The rule applies to methods carrying a test attribute; helper and test-double
/// members are ignored. The pattern is enforced for unit and UI test assemblies.
/// </summary>
public sealed class TestNamingRules
{
    /// <summary>Attributes that mark a method as a test.</summary>
    private static readonly string[] TestAttributes =
        ["TestAttribute", "TestCaseAttribute", "AvaloniaTestAttribute"];

    private static readonly Regex NamePattern = new(
        @"^\w+_When\w+_Should\w+$",
        RegexOptions.CultureInvariant);

    [Test]
    public void Unit_test_methods_follow_the_naming_pattern()
    {
        AssertNamesFollowPattern(".Tests.Unit");
    }

    [Test]
    public void Ui_test_methods_follow_the_naming_pattern()
    {
        AssertNamesFollowPattern(".Tests.UI");
    }

    private static void AssertNamesFollowPattern(string assemblySuffix)
    {
        var testMethods = SolutionArchitecture.Instance.Types
            .Where(t => t.Assembly.Name.EndsWith(assemblySuffix))
            .SelectMany(t => t.Members.OfType<MethodMember>())
            .Where(IsTestMethod)
            // ArchUnitNET reports the signature; the rule is about the name only.
            .Select(m => m.Name.Split('(')[0])
            .OrderBy(name => name)
            .ToList();

        testMethods.Should().NotBeEmpty($"the {assemblySuffix} projects define test methods");

        var offenders = testMethods
            .Where(name => !NamePattern.IsMatch(name))
            .Select(name => $"  {name}")
            .ToList();

        offenders.Should().BeEmpty(
            "test methods must be named "
            + "<ComponentTested>_When<StateCondition>_Should<ExpectedResult>, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsTestMethod(MethodMember method) =>
        method.AttributeInstances.Any(a => TestAttributes.Contains(a.Type.Name));
}
