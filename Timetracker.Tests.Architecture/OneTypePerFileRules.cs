using System.Text.RegularExpressions;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// A source file declares at most one top-level type — the rule StyleCop's SA1402
/// enforces. Nested types are allowed and ignored. The repository uses file-scoped
/// namespaces, so a top-level declaration always starts in column zero; the rule
/// reads that column instead of parsing C#, and generated files under obj/ and bin/
/// are skipped. There is no StyleCop analyzer in the build, so this rule is what
/// keeps the convention from drifting.
/// </summary>
public sealed class OneTypePerFileRules
{
    private static readonly Regex TopLevelTypePattern = new(
        @"^(?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly|ref|unsafe|file|new)\s+)*\b(?:class|interface|struct|record|enum|delegate)\b",
        RegexOptions.CultureInvariant);

    [Test]
    public void Source_files_declare_at_most_one_top_level_type()
    {
        var root = SolutionFiles.RepositoryRoot();
        var offenders = SourceFiles()
            .Select(file => (File: file, Count: TopLevelTypeCount(file)))
            .Where(entry => entry.Count > 1)
            .Select(entry => $"  {Path.GetRelativePath(root, entry.File)}: {entry.Count} top-level types")
            .OrderBy(line => line)
            .ToList();

        offenders.Should().BeEmpty("each source file must declare one top-level type, but found:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static IReadOnlyList<string> SourceFiles() =>
        [.. Directory
            .EnumerateFiles(SolutionFiles.RepositoryRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .OrderBy(path => path)];

    private static bool IsGenerated(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar);
        var result = segments.Contains("obj") || segments.Contains("bin");
        return result;
    }

    private static int TopLevelTypeCount(string file) =>
        File.ReadLines(file)
            .Count(line => line.Length > 0 && !char.IsWhiteSpace(line[0]) && TopLevelTypePattern.IsMatch(line));
}
