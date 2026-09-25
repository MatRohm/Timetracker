using ArchUnitNET.Domain;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Every interface of a production project lives in that project's
/// <c>Interfaces</c> namespace (<c>&lt;RootNamespace&gt;.Interfaces</c>), so the
/// contracts of a project are found in one place. This applies to public and
/// internal interfaces alike; test projects are exempt.
/// </summary>
public sealed class InterfaceNamespaceRules
{
    /// <summary>The namespace suffix interfaces must live in.</summary>
    private const string InterfaceNamespaceSuffix = ".Interfaces";

    [Test]
    public void Production_interfaces_live_in_an_interfaces_namespace()
    {
        var offenders = ProductionInterfaces()
            .Where(i => !IsInInterfacesNamespace(i))
            .Select(i => $"  {i.FullName}: expected {ExpectedNamespace(i)}")
            .OrderBy(text => text)
            .ToList();

        offenders.Should().BeEmpty(
            "every production interface must live in its project's .Interfaces namespace, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void There_are_interfaces_to_check()
    {
        // Guards against the rule passing because no interfaces were discovered.
        ProductionInterfaces().Should().NotBeEmpty("the solution defines production interfaces");
    }

    private static IEnumerable<Interface> ProductionInterfaces() =>
        SolutionArchitecture.Instance.Interfaces
            .Where(i => i.Assembly.Name.StartsWith("Timetracker"))
            .Where(i => !i.Assembly.Name.Contains(".Tests."))
            .Where(i => !i.IsStub && !i.IsNested);

    private static bool IsInInterfacesNamespace(Interface type) =>
        type.Namespace?.Name.EndsWith(InterfaceNamespaceSuffix, StringComparison.Ordinal) == true;

    /// <summary>The namespace the interface should live in (its assembly's root + .Interfaces).</summary>
    private static string ExpectedNamespace(Interface type) =>
        type.Assembly.Name + InterfaceNamespaceSuffix;
}
