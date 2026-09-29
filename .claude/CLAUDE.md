# Agent Instructions

> This file is shared by Claude Code and opencode. Claude Code reads it directly;
> opencode loads it through `instructions` in the root `opencode.json`, and reads
> the skills from `.claude/skills` natively. Keep agent configuration here rather
> than adding copies elsewhere; the architecture test `AgentConfigurationRules`
> fails when a duplicate or another agent's configuration appears.

This repository contains a .NET/C# application.

## General behavior

- Inspect the existing implementation before making changes.
- Follow existing naming, architecture, and coding patterns.
- Make the smallest change necessary to satisfy the task.
- Do not introduce new NuGet packages unless necessary.
- Do not change public APIs unless required by the task.
- Do not modify unrelated files.
- Do not suppress compiler or analyzer warnings without explaining why.

## C# guidelines

- Follow the repository's .editorconfig.
- Respect nullable reference type annotations.
- Prefer clear, idiomatic modern C#.
- Prefer file-scoped namespaces where consistent with the project.
- Use async/await for asynchronous I/O.
- Do not use `.Result` or `.Wait()` on Tasks.
- Methods returning Task should normally use the Async suffix.
- Accept CancellationToken where an operation can reasonably be cancelled.
- Prefer dependency injection over service locator patterns.
- Prefer immutable data where practical.
- Avoid unnecessary abstractions and premature generalization.
- Assign the return value to a named variable before returning, so the
  value is self-documenting and easy to inspect in the debugger.
  Write `var result = Compute(); return result;` rather than
  `return Compute();`. Trivial returns (`return;`, constants, direct
  field/property reads, expression-bodied members) are exempt.

## Architecture

The solution follows these dependency rules:

Domain
    ↑
Application
    ↑
Infrastructure / API

- Domain must not reference Infrastructure.
- Domain must not depend on database, HTTP, or UI concerns.
- Application contains use cases and orchestration.
- Infrastructure implements external concerns such as persistence.
- API/controllers/endpoints should contain minimal business logic.

Before modifying architecture, inspect existing projects and dependencies.

## Testing

When changing behavior:

- Add or update tests.
- Follow the existing test framework and conventions.
- Test externally observable behavior rather than implementation details.
- Do not delete, skip, or weaken tests merely to make them pass.
- Prefer focused tests relevant to the change.

## Validation

After modifying C# code, run:

    dotnet build

Then run:

    dotnet test

If formatting is configured, also run:

    dotnet format --verify-no-changes

If a command fails:

1. Read the error.
2. Determine whether the failure was caused by your changes.
3. Fix problems caused by your changes.
4. Run the relevant command again.

Do not claim that a change works unless it has been built/tested,
or explicitly state that validation could not be performed.

## Git

- Whenever a coding task is finished, you are allowed to commit the work
  in sensible, separate commits.
- **Do not push to remotes without explicit permission from the user** —
  pushing happens only when the user asks for it.
- Commit messages must follow Conventional Commits **and end with the tracked
  issue id** (e.g. `feat: show the week view as a tree (timetracker-66k)`); both
  are enforced by the shared `commit-msg` hook (see README.md for the setup).

## Before creating new code

Before creating a new:

- service
- interface
- repository
- DTO
- exception
- utility
- extension method

search the repository for an existing equivalent.

Prefer reusing or extending existing components over creating duplicates.

## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Issues live in a local Dolt
database; sync uses `refs/dolt/data` on the git remote; `.beads/issues.jsonl` is a
passive export. See
https://github.com/gastownhall/beads/blob/main/docs/core-concepts/sync-concepts.md
for details and anti-patterns. The `beads` skill has the full workflow.

### Quick Reference

```bash
bd prime                # Load the full workflow context
bd ready                # Find available work
bd show <id>            # View issue details
bd update <id> --claim  # Claim work
bd close <id>           # Complete work
```

### Rules

- Run `bd prime` at the start of every session and whenever beads context is
  missing or stale (Claude Code loads it automatically through its SessionStart
  hook; opencode does not).
- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown
  TODO lists.
- Use `bd remember` for persistent knowledge — do NOT create ad hoc memory files
  such as MEMORY.md.
- Every task commit must have a beads issue associated with it; if none is
  given, create one yourself. Committing against a closed issue is allowed when
  a fix for that issue is needed.

## Agent Context Profiles

The Beads guidance is task-tracking guidance, not permission to override
repository, user, or orchestrator instructions.

- **Conservative (default)**: Use `bd` for task tracking. Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.
- **Minimal**: Keep tool instruction files as pointers to `bd prime`; use the same conservative git policy unless active instructions say otherwise.
- **Team-maintainer**: Only when the repository explicitly opts in, agents may close beads, run quality gates, commit, and push as part of session close. A current "do not commit" or "do not push" instruction still wins.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Handle git/sync by active profile**:
   ```bash
   # Conservative/minimal/default: report status and proposed commands; wait for approval.
   git status

   # Team-maintainer opt-in only, unless current instructions forbid it:
   git pull --rebase
   bd dolt push
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads guidance.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
