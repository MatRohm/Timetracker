using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using AwesomeAssertions;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// MVVM layering rules for the Timetracker application: views may only depend on
/// view models, view models may only depend on models, and view models never touch
/// Avalonia. "Depend on" is restricted to <c>Timetracker.*</c> namespaces; the .NET
/// base class library and third-party frameworks (<c>System.*</c>, <c>Microsoft.*</c>,
/// and <c>Avalonia.*</c> inside views) are always allowed.
/// </summary>
public sealed class MvvmRules
{
    [Test]
    public void View_models_do_not_depend_on_avalonia()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.ViewModels(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Avalonia(\..*)?$"))
            .Because("view models must stay UI-agnostic and testable without Avalonia");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void View_models_depend_only_on_models()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.ViewModels(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Timetracker\.(?!Models(\..*)?$)(?!ViewModels(\..*)?$).+$"))
            .Because("view models may only depend on Timetracker.Models");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void Views_depend_only_on_view_models()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Timetracker\.Views(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Timetracker\.(?!ViewModels(\..*)?$)(?!Views(\..*)?$).+$"))
            .Because("views may only depend on Timetracker.ViewModels");

        rule.Check(SolutionArchitecture.Instance);
    }

    [Test]
    public void There_are_view_models_and_views_to_check()
    {
        // Guards against the rules passing because no types were discovered.
        var types = SolutionArchitecture.Instance.Types.ToList();

        types.Count(t => t.Namespace?.Name.StartsWith("Timetracker.ViewModels", StringComparison.Ordinal) == true)
            .Should().BePositive("the app defines view models");
        types.Count(t => t.Namespace?.Name.StartsWith("Timetracker.Views", StringComparison.Ordinal) == true)
            .Should().BePositive("the app defines views");
    }
}
