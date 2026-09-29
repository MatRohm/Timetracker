using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Rules for the AI coding-agent configuration. Only Claude Code (<c>.claude/</c>)
/// and opencode (<c>.opencode/</c>, plus the root <c>opencode.json</c> it requires)
/// are configured, and both carry the same content: identical instructions
/// (<c>.claude/CLAUDE.md</c> and <c>.opencode/AGENTS.md</c>) and identical skills.
/// The copies are plain duplicates, so these rules are what keeps them in sync.
/// </summary>
public sealed class AgentConfigurationRules
{
    private const string ClaudeInstructions = ".claude/CLAUDE.md";
    private const string OpencodeInstructions = ".opencode/AGENTS.md";
    private const string ClaudeSkills = ".claude/skills";
    private const string OpencodeSkills = ".opencode/skills";

    /// <summary>
    /// Configuration of other agents (and the former root instruction files, now
    /// kept inside the two tool directories) that must not come back.
    /// </summary>
    private static readonly string[] RemovedConfigurations =
    [
        ".agents", ".codex", ".cursor", ".omp", ".gemini", ".windsurf", ".kiro",
        "AGENTS.md", "CLAUDE.md", "GEMINI.md", ".github/copilot-instructions.md",
    ];

    [Test]
    public void Claude_and_opencode_instructions_are_identical()
    {
        var claude = ReadText(ClaudeInstructions);
        var opencode = ReadText(OpencodeInstructions);

        opencode.Should().Be(claude,
            $"{OpencodeInstructions} must be an exact copy of {ClaudeInstructions}; edit both together");
    }

    [Test]
    public void Claude_and_opencode_skills_are_identical()
    {
        var claude = SkillFiles(ClaudeSkills);
        var opencode = SkillFiles(OpencodeSkills);

        opencode.Should().Equal(claude,
            $"{OpencodeSkills} must contain the same files as {ClaudeSkills}");

        var differing = claude
            .Where(file => ReadText($"{ClaudeSkills}/{file}") != ReadText($"{OpencodeSkills}/{file}"))
            .ToList();
        differing.Should().BeEmpty(
            $"every skill file must be identical in {ClaudeSkills} and {OpencodeSkills}, but these differ: "
            + string.Join(", ", differing));
    }

    [Test]
    public void There_are_skills_to_compare()
    {
        // Pins that the comparison above is not vacuously true.
        SkillFiles(ClaudeSkills).Should().Contain(["beads/SKILL.md", "solid/SKILL.md"]);
    }

    [Test]
    public void Opencode_config_loads_the_shared_instructions()
    {
        // opencode reads instruction files inside .opencode/ only when the root
        // config lists them; without this entry it would get no instructions at all.
        using var config = JsonDocument.Parse(ReadText("opencode.json"));
        var instructions = config.RootElement.TryGetProperty("instructions", out var list)
            ? list.EnumerateArray().Select(e => e.GetString()).ToList()
            : [];

        instructions.Should().Contain(OpencodeInstructions,
            $"opencode.json must list {OpencodeInstructions} under \"instructions\"");
    }

    [Test]
    public void Only_the_claude_and_opencode_configurations_exist()
    {
        var root = SolutionFiles.RepositoryRoot();
        var offenders = RemovedConfigurations
            .Where(path => File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path)))
            .ToList();

        offenders.Should().BeEmpty(
            "only .claude/ and .opencode/ (plus the root opencode.json) configure AI agents, but found: "
            + string.Join(", ", offenders));
    }

    /// <summary>Reads a repository file with line endings normalized, so checkout settings do not matter.</summary>
    private static string ReadText(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(SolutionFiles.RepositoryRoot(), relativePath));
        var result = text.Replace("\r\n", "\n");
        return result;
    }

    /// <summary>Every file below a skills directory, as sorted '/'-separated relative paths.</summary>
    private static List<string> SkillFiles(string relativeDirectory)
    {
        var directory = Path.Combine(SolutionFiles.RepositoryRoot(), relativeDirectory);
        var result = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
        return result;
    }
}
