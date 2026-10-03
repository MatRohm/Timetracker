using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using AwesomeAssertions;
using NUnit.Framework;
using System.Text.RegularExpressions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// MVVM layering rules for the Timetracker application: views may only depend on
/// view models, view models may only depend on models, services and their
/// interfaces, and view models (in any project) never touch Avalonia. Neither may depend on the
/// composition root in the <c>Timetracker.App</c> namespace itself (<c>App</c>,
/// <c>HookRegistry</c>, <c>Program</c>). "Depend on" is restricted to <c>Timetracker.App</c>
/// and its sub-namespaces; the .NET base class library and third-party frameworks
/// (<c>System.*</c>, <c>Microsoft.*</c>, and <c>Avalonia.*</c> inside views) are always allowed.
/// </summary>
public sealed class MvvmRules
{
    /// <summary>A <c>ViewModels</c> namespace (or below) of any Timetracker project.</summary>
    private static readonly Regex ViewModelNamespace = new(@"^Timetracker(\.[^.]+)*\.ViewModels(\..*)?$");

    [Test]
    public void View_models_do_not_depend_on_avalonia()
    {
        // Applies to every project's view models, the plug-ins' included. Checked on the
        // dependency targets directly: Avalonia is only loaded as stubs, which a fluent
        // Types().That() selector does not match, so such a rule would never fail.
        var offenders = SolutionArchitecture.Instance.Types
            .Where(type => !type.Assembly.Name.Contains(".Tests."))
            .Where(type => ViewModelNamespace.IsMatch(type.Namespace.FullName))
            .Where(type => type.Dependencies.Any(dependency =>
                dependency.Target.Namespace.FullName == "Avalonia"
                || dependency.Target.Namespace.FullName.StartsWith("Avalonia.", StringComparison.Ordinal)))
            .Select(type => $"  {type.FullName}")
            .Distinct()
            .OrderBy(text => text)
            .ToList();

        offenders.Should().BeEmpty(
            "view models must stay UI-agnostic and testable without Avalonia, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void View_models_depend_only_on_models_services_and_interfaces()
    {
        // Services are mostly consumed through their interfaces (ITrackerRepository,
        // IUiTimer, ...), so both are allowed; views stay off-limits. The pattern also
        // matches the root namespace itself, where the composition root lives.
        // Localization is allowed for the same reason as Services: it is a leaf of
        // resource lookups with no Avalonia, I/O or composition-root dependency, and
        // both views and view models show user-visible text (decided 2026-10-03,
        // timetracker-lsr).
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.App\.ViewModels(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Timetracker\.App(\.(?!(Models|ViewModels|Services|Interfaces|Localization)(\..*)?$).+)?$"))
            .Because("view models may only depend on Timetracker.App.Models, .Services and .Interfaces, "
                + "never on the composition root (App, HookRegistry, Program)");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void Views_depend_only_on_view_models()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.App\.Views(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Timetracker\.App(\.(?!(ViewModels|Views|Localization)(\..*)?$).+)?$"))
            .Because("views may only depend on Timetracker.App.ViewModels, "
                + "never on the composition root (App, HookRegistry, Program)");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void There_are_view_models_and_views_to_check()
    {
        // Guards against the rules passing because no types were discovered.
        var types = SolutionArchitecture.Instance.Types.ToList();

        types.Count(t => t.Namespace?.Name.StartsWith("Timetracker.App.ViewModels", StringComparison.Ordinal) == true)
            .Should().BePositive("the app defines view models");
        types.Count(t => t.Namespace?.Name.StartsWith("Timetracker.App.Views", StringComparison.Ordinal) == true)
            .Should().BePositive("the app defines views");
    }
}
