namespace Timetracker.Tests.Architecture;

/// <summary>
/// Derives a production assembly's root namespace from its assembly name. The app's
/// assembly is named "Timetracker" but its namespaces use the project name
/// "Timetracker.App"; every other project's root namespace matches its assembly name.
/// </summary>
internal static class ProductionNamespaces
{
    private const string AppAssemblyName = "Timetracker";
    private const string AppRootNamespace = "Timetracker.App";

    public static string RootNamespace(string assemblyName) =>
        assemblyName == AppAssemblyName ? AppRootNamespace : assemblyName;
}
