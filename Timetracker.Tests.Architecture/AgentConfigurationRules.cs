using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Tests.Architecture;

/// <summary>
/// Rules for the AI coding-agent configuration. Claude Code and opencode share one
/// set of files in <c>.claude/</c>: the instructions (<c>.claude/CLAUDE.md</c>,
/// which opencode loads through a root <c>AGENTS.md</c> symlink), the skills
/// (<c>.claude/skills</c>, which opencode reads natively) and the subagents
/// (<c>.claude/agents</c>, which opencode gets through <c>agent</c> entries in
/// <c>opencode.json</c> that point at the same files). There are no copies to
/// keep in sync, so these rules keep duplicates and other agents'
/// configurations from coming back.
/// </summary>
public sealed class AgentConfigurationRules
{
    private const string SharedInstructions = ".claude/CLAUDE.md";
    private const string SharedSkills = ".claude/skills";
    private const string SharedAgents = ".claude/agents";
    private const string AgentsFile = "AGENTS.md";

    /// <summary>
    /// Configuration that must not come back: other agents' files, the former root
    /// instruction files, and opencode-only copies of the shared instructions or
    /// skills (opencode would see every skill twice). <c>AGENTS.md</c> is expected
    /// to exist as the symlink the <see cref="Agents_md_points_to_the_shared_instructions"/>
    /// rule verifies.
    /// </summary>
    private static readonly string[] RemovedConfigurations =
    [
        ".agents", ".codex", ".cursor", ".omp", ".gemini", ".windsurf", ".kiro",
        "CLAUDE.md", "GEMINI.md", ".github/copilot-instructions.md",
        ".opencode/AGENTS.md", ".opencode/skills", ".opencode/agents",
    ];

    [Test]
    public void Agents_md_points_to_the_shared_instructions()
    {
        // opencode V2 reads AGENTS.md only (it never falls back to CLAUDE.md, and its
        // `instructions` config is not resolved), so the root AGENTS.md must be a
        // symlink to the shared instructions rather than a duplicate copy.
        var root = SolutionFiles.RepositoryRoot();
        var link = new FileInfo(Path.Combine(root, AgentsFile));

        link.Exists.Should().BeTrue(
            $"{AgentsFile} must exist so opencode loads the shared instructions");
        link.LinkTarget.Should().NotBeNull(
            $"{AgentsFile} must be a symlink, not a duplicate copy");

        var resolved = link.ResolveLinkTarget(returnFinalTarget: true);
        resolved.Should().NotBeNull(
            $"{AgentsFile} must resolve to {SharedInstructions}");
        Path.GetFullPath(resolved!.FullName).Should().Be(
            Path.GetFullPath(Path.Combine(root, SharedInstructions)),
            $"{AgentsFile} must point at {SharedInstructions} so there is no copy to keep in sync");
    }

    [Test]
    public void The_shared_instructions_and_skills_exist()
    {
        var root = SolutionFiles.RepositoryRoot();

        File.Exists(Path.Combine(root, SharedInstructions)).Should().BeTrue(
            $"{SharedInstructions} holds the instructions for both agents");
        foreach (var skill in new[] { "beads", "solid" })
        {
            File.Exists(Path.Combine(root, SharedSkills, skill, "SKILL.md")).Should().BeTrue(
                $"the {skill} skill must live in {SharedSkills}, where both agents read it");
        }
    }

    [TestCase("architecture-review")]
    public void Opencode_config_uses_the_shared_agent_definition(string agent)
    {
        // The subagent is defined once in .claude/agents; opencode must load that
        // file as the prompt instead of carrying its own copy.
        var definition = $"{SharedAgents}/{agent}.md";
        File.Exists(Path.Combine(SolutionFiles.RepositoryRoot(), definition)).Should().BeTrue(
            $"the {agent} agent must be defined in {SharedAgents}");

        using var config = JsonDocument.Parse(ReadText("opencode.json"));
        var prompt = config.RootElement.TryGetProperty("agent", out var agents)
                     && agents.TryGetProperty(agent, out var entry)
                     && entry.TryGetProperty("prompt", out var value)
            ? value.GetString()
            : null;

        prompt.Should().Be($"{{file:./{definition}}}",
            $"opencode.json must load the {agent} agent from {definition}");
    }

    [Test]
    public void Only_the_shared_agent_configuration_exists()
    {
        var root = SolutionFiles.RepositoryRoot();
        var offenders = RemovedConfigurations
            .Where(path => File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path)))
            .ToList();

        offenders.Should().BeEmpty(
            "AI agents are configured only through .claude/ and the root opencode.json, but found: "
            + string.Join(", ", offenders));
    }

    private static string ReadText(string relativePath)
    {
        var result = File.ReadAllText(Path.Combine(SolutionFiles.RepositoryRoot(), relativePath));
        return result;
    }

}
