using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Logging rules: production code logs only through
/// <c>Microsoft.Extensions.Logging.ILogger</c>. Nothing appends to a log file by hand
/// (the former static <c>ErrorLog</c>, <c>ErrorLogAdapter</c> and <c>MonitorLog</c>
/// did), and Serilog, which writes the files, is used only by the shared setup in
/// <c>Timetracker.Plugins.Contracts.Logging</c>. Test projects are exempt.
/// </summary>
public sealed class LoggingRules
{
    private const string LoggingSetupNamespace = "Timetracker.Plugins.Contracts.Logging";

    [Test]
    public void Production_code_does_not_append_to_files_by_hand()
    {
        var offenders = ProductionTypes()
            .SelectMany(type => type.Dependencies.OfType<MethodCallDependency>()
                .Where(IsFileAppend)
                .Select(call => $"  {type.FullName} calls File.{call.TargetMember.Name}"))
            .Distinct()
            .OrderBy(text => text)
            .ToList();

        offenders.Should().BeEmpty(
            "production code logs through ILogger (FileLogging writes the files) instead of appending "
            + "to a log file itself, but found:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void Only_the_shared_logging_setup_uses_serilog()
    {
        var offenders = ProductionTypes()
            .Where(type => type.Namespace.FullName != LoggingSetupNamespace)
            .Where(UsesSerilog)
            .Select(type => $"  {type.FullName}")
            .OrderBy(text => text)
            .ToList();

        offenders.Should().BeEmpty(
            $"only {LoggingSetupNamespace} may use Serilog; everything else logs through ILogger, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void The_shared_logging_setup_uses_serilog()
    {
        // Guards against the Serilog rule passing because the dependency is not seen.
        ProductionTypes()
            .Where(type => type.Namespace.FullName == LoggingSetupNamespace)
            .Should().Contain(type => UsesSerilog(type), "FileLogging configures Serilog");
    }

    private static IEnumerable<IType> ProductionTypes() =>
        SolutionArchitecture.Instance.Types
            .Where(type => type.Assembly.Name.StartsWith("Timetracker"))
            .Where(type => !type.Assembly.Name.Contains(".Tests."))
            .Where(type => !type.IsStub);

    private static bool IsFileAppend(MethodCallDependency call) =>
        call.TargetMember.DeclaringType.FullName == "System.IO.File"
        && call.TargetMember.Name.StartsWith("Append", StringComparison.Ordinal);

    private static bool UsesSerilog(IType type) =>
        type.Dependencies.Any(dependency =>
            dependency.Target.Namespace is { } ns
            && (ns.FullName == "Serilog"
                || ns.FullName.StartsWith("Serilog.", StringComparison.Ordinal)));
}
