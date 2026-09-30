using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using AwesomeAssertions;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// MVVM layering rules for the Timetracker application: views may only depend on
/// view models, view models may only depend on models, services and their
/// interfaces, and view models never touch Avalonia. "Depend on" is restricted to <c>Timetracker.App.*</c> namespaces; the .NET
/// base class library and third-party frameworks (<c>System.*</c>, <c>Microsoft.*</c>,
/// and <c>Avalonia.*</c> inside views) are always allowed.
/// </summary>
public sealed class MvvmRules
{
    [Test]
    public void View_models_do_not_depend_on_avalonia()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.App\.ViewModels(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Avalonia(\..*)?$"))
            .Because("view models must stay UI-agnostic and testable without Avalonia");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void View_models_depend_only_on_models_services_and_interfaces()
    {
        // Services are mostly consumed through their interfaces (ITrackerRepository,
        // IUiTimer, ...), so both are allowed; views stay off-limits.
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.App\.ViewModels(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Timetracker\.App\.(?!Models(\..*)?$)(?!ViewModels(\..*)?$)(?!Services(\..*)?$)(?!Interfaces(\..*)?$).+$"))
            .Because("view models may only depend on Timetracker.App.Models, .Services and .Interfaces");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void Views_depend_only_on_view_models()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.App\.Views(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Timetracker\.App\.(?!ViewModels(\..*)?$)(?!Views(\..*)?$).+$"))
            .Because("views may only depend on Timetracker.App.ViewModels");

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
