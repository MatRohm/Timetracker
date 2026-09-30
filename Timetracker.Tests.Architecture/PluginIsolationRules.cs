using ArchUnitNET.Domain;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Plugin isolation: only the composition root (<c>Timetracker.App.HookRegistry</c>)
/// may reference the concrete plugin projects (AzureDevOps, ActivityMonitor, WeekView).
/// Every other app type goes through the shared <c>Timetracker.Plugins.Contracts</c>.
/// Checked on the dependency targets directly, so generic-type arguments and method
/// bodies are seen and the "plugins except Contracts" scope stays precise.
/// </summary>
public sealed class PluginIsolationRules
{
    private const string AppAssemblyName = "Timetracker";
    private const string AppNamespace = "Timetracker.App";
    private const string HookRegistryFullName = "Timetracker.App.HookRegistry";
    private const string ContractsNamespace = "Timetracker.Plugins.Contracts";

    [Test]
    public void Only_the_hook_registry_references_plugins()
    {
        var offenders = AppTypes()
            .Where(type => !IsHookRegistry(type))
            .SelectMany(type => PluginDependencies(type)
                .Select(target => $"  {type.FullName} -> {target.FullName}"))
            .Distinct()
            .OrderBy(text => text)
            .ToList();

        offenders.Should().BeEmpty(
            "only Timetracker.App.HookRegistry may reference the concrete plugins; every other app "
            + "type must go through Timetracker.Plugins.Contracts, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void The_hook_registry_references_a_plugin()
    {
        // Guards against the rule passing because the plugin dependency is not seen.
        var hookRegistry = SolutionArchitecture.Instance.Types
            .First(type => type.FullName == HookRegistryFullName);

        PluginDependencies(hookRegistry).Should().NotBeEmpty(
            "HookRegistry wires the plugins, so the rule cannot pass vacuously");
    }

    /// <summary>App types (namespace Timetracker.App) of the Timetracker assembly.</summary>
    private static IEnumerable<IType> AppTypes() =>
        SolutionArchitecture.Instance.Types
            .Where(type => type.Assembly.Name == AppAssemblyName)
            .Where(type => !type.IsStub)
            .Where(type => IsAppNamespace(type.Namespace?.FullName));

    /// <summary>HookRegistry itself or one of its compiler-generated nested types (the DI lambdas).</summary>
    private static bool IsHookRegistry(IType type) =>
        type.FullName == HookRegistryFullName
        || (type.IsNested && type.IsCompilerGenerated
            && type.FullName.StartsWith(HookRegistryFullName + "+", StringComparison.Ordinal));

    /// <summary>The plugin types (outside Contracts) that <paramref name="type"/> depends on.</summary>
    private static IEnumerable<IType> PluginDependencies(IType type) =>
        type.Dependencies
            .Select(dependency => dependency.Target)
            .Where(target => IsPluginNamespace(target.Namespace?.FullName)
                && !IsContractsNamespace(target.Namespace?.FullName));

    private static bool IsAppNamespace(string? ns) =>
        ns is not null && (ns == AppNamespace || ns.StartsWith(AppNamespace + ".", StringComparison.Ordinal));

    private static bool IsPluginNamespace(string? ns) =>
        ns is not null && (ns == "Timetracker.Plugins" || ns.StartsWith("Timetracker.Plugins.", StringComparison.Ordinal));

    private static bool IsContractsNamespace(string? ns) =>
        ns is not null && (ns == ContractsNamespace || ns.StartsWith(ContractsNamespace + ".", StringComparison.Ordinal));
}
