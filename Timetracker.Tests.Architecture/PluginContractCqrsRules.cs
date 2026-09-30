using ArchUnitNET.Domain;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// The plugin contract interfaces are CQRS-shaped: every interface is named either
/// <c>…Query</c> (reads/returns data) or <c>…Command</c> (mutates/acts), so its
/// role is explicit and machine-checked. Only the contracts project is checked;
/// other projects' interfaces follow their own conventions.
/// </summary>
public sealed class PluginContractCqrsRules
{
    [Test]
    public void Plugin_contract_interfaces_are_named_query_or_command()
    {
        var offenders = PluginContractInterfaces()
            .Where(i => !IsQueryOrCommand(i))
            .Select(i => $"  {i.FullName}")
            .OrderBy(text => text)
            .ToList();

        offenders.Should().BeEmpty(
            "every plugin contract interface must be named …Query or …Command, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void There_are_plugin_contract_interfaces_to_check()
    {
        // Guards against the rule passing because no contract interfaces were discovered.
        PluginContractInterfaces().Should().NotBeEmpty("the contracts project defines interfaces");
    }

    private static IEnumerable<Interface> PluginContractInterfaces() =>
        SolutionArchitecture.Instance.Interfaces
            .Where(i => i.Assembly.Name == "Timetracker.Plugins.Contracts")
            .Where(i => !i.IsStub && !i.IsNested);

    private static bool IsQueryOrCommand(Interface type) =>
        type.Name.EndsWith("Query", StringComparison.Ordinal)
        || type.Name.EndsWith("Command", StringComparison.Ordinal);
}
