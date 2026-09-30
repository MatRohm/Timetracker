using ArchUnitNET.Domain;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Every production project keeps its MVVM layers apart by namespace: views
/// (Avalonia controls such as windows, user controls and panels) live in the
/// project's <c>Views</c> namespace, view models (non-Avalonia types implementing
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/>) in its
/// <c>ViewModels</c> namespace, or in a sub-namespace of either. Test projects are exempt.
/// Types are recognised by their interfaces: ArchUnitNET loads Avalonia only as stubs,
/// so a view's base-class chain ends at its direct Avalonia base class.
/// </summary>
public sealed class ViewPlacementRules
{
    /// <summary>Implemented by every Avalonia control, but not by the <c>Application</c>.</summary>
    private const string AvaloniaLogical = "Avalonia.LogicalTree.ILogical";
    private const string NotifyPropertyChanged = "System.ComponentModel.INotifyPropertyChanged";

    [Test]
    public void Views_reside_in_a_views_namespace()
    {
        var offenders = Offenders(ProductionViews(), ".Views");

        offenders.Should().BeEmpty(
            "every production view must live in its project's .Views namespace, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void View_models_reside_in_a_view_models_namespace()
    {
        var offenders = Offenders(ProductionViewModels(), ".ViewModels");

        offenders.Should().BeEmpty(
            "every production view model must live in its project's .ViewModels namespace, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Test]
    public void There_are_views_and_view_models_to_check()
    {
        // Guards against the rules passing because the base types were not recognised:
        // the app and the activity monitor both define views and view models.
        var viewAssemblies = ProductionViews().Select(c => c.Assembly.Name).Distinct().ToList();
        var viewModelAssemblies = ProductionViewModels().Select(c => c.Assembly.Name).Distinct().ToList();

        viewAssemblies.Should().Contain(["Timetracker", "Timetracker.Plugins.ActivityMonitor"]);
        viewModelAssemblies.Should().Contain(["Timetracker", "Timetracker.Plugins.ActivityMonitor"]);
    }

    private static IEnumerable<Class> ProductionViews() =>
        ProductionClasses().Where(c => c.ImplementedInterfaces.Any(i => i.FullName == AvaloniaLogical));

    private static IEnumerable<Class> ProductionViewModels() =>
        // Avalonia objects (controls, the application) notify too, but are no view models.
        ProductionClasses()
            .Where(c => c.ImplementedInterfaces.Any(i => i.FullName == NotifyPropertyChanged))
            .Where(c => !c.ImplementedInterfaces.Any(i => i.FullName.StartsWith("Avalonia.", StringComparison.Ordinal)));

    private static IEnumerable<Class> ProductionClasses() =>
        SolutionArchitecture.Instance.Classes
            .Where(c => c.Assembly.Name.StartsWith("Timetracker"))
            .Where(c => !c.Assembly.Name.Contains(".Tests."))
            .Where(c => !c.IsStub && !c.IsCompilerGenerated);

    /// <summary>The classes outside <c>&lt;RootNamespace&gt;&lt;suffix&gt;</c> and its sub-namespaces.</summary>
    private static List<string> Offenders(IEnumerable<Class> classes, string suffix)
    {
        var result = classes
            .Select(c => (Class: c, Expected: ProductionNamespaces.RootNamespace(c.Assembly.Name) + suffix))
            .Where(x => !IsInOrBelow(x.Class.Namespace?.Name, x.Expected))
            .Select(x => $"  {x.Class.FullName}: expected {x.Expected}")
            .OrderBy(text => text)
            .ToList();
        return result;
    }

    private static bool IsInOrBelow(string? name, string expected) =>
        name == expected || name?.StartsWith(expected + ".", StringComparison.Ordinal) == true;
}
