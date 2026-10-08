---
name: development
description: Use when planning or implementing a feature or task in this repository, from describing and planning the work through implementation, review and finishing. Provides the four-phase development process (plan, implement, review, finish) wired to the repo's beads tracking, branch and commit conventions, quality gates and the architecture review.
---

# Development

The end-to-end process for taking a feature or task from description to a
merged, pushed change. Four phases: **plan** (A), **implement** (B), **review**
(C), and **finish** (D).

**Phase gate** — at the end of every phase, stop and wait for an explicit prompt
from the user before advancing to the next phase (`A` → `B` → `C` → `D`). Never
begin a phase until the user has said to move on.

## A) Feature / task description & planning

1. **Load context** — run `bd prime` at the start of the session (opencode does
   not auto-load it, unlike Claude Code).
2. **Find or create the beads issue** — `bd ready` / `bd list` for existing
   work; `bd create "…" --description="…" --type=task` for new. The
   **description** holds *what and why*; the **plan goes in the `design` field**
   (`--design` / `--design-file`), never in the description.
3. **State the plan fast** — briefly: what changes, which files, how it is
   verified.
4. **Explore before proposing** — inspect the existing implementation and
   *search for an existing equivalent* before introducing any new service,
   interface, repository, DTO, exception, utility, or extension method.
5. **Respect the micro-kernel architecture** — `Timetracker.App` is the
   kernel; plugins extend it only through `Timetracker.Plugins.Contracts`.
   Decide which project(s) are affected (`Timetracker.App`, a `*.Plugins.*`,
   or `Timetracker.Plugins.Contracts` for a new hook).
6. **Write acceptance criteria first** — "do I understand the requirement?"
7. **Close out the phase** — the beads issue is now **created and pushed**
   (`bd dolt push`). If an issue already existed with a described plan, **verify
   the plan still holds** against what exploration found; if it no longer fits,
   **revise the plan and update the issue** rather than leaving it stale.

   **Stop here** — wait for the user to explicitly advance to phase B.

## B) Implementation

1. **Claim the beads issue** — `bd update <id> --claim`, which atomically sets
   you as the assignee and moves the status to `in_progress`. Do this before
   writing any code so other agents don't pick up the same work.
2. **Check out a branch for the work** — name it so it references the beads
   issue, e.g. `timetracker-66k-week-view` (`<issue-id>-<short-slug>`).
3. **TDD (red-green-refactor)** — write the failing test before production
   code; test *externally observable behavior*, not implementation details.
4. **Follow C# conventions** — `.editorconfig` (file-scoped namespaces,
   explicit types, `_camelCase` fields, `I`-prefixed interfaces), async/await
   (no `.Result`/`.Wait()`, `Async` suffix, `CancellationToken` where
   reasonable), DI over service locators, immutable data where practical.
5. **Test naming** — `<Method>_When<Condition>_Should<Result>`; the architecture
   tests report violations.
6. **Match the architecture** — MVVM (view models know nothing about Avalonia,
   views hold no logic); new plugin behavior via `IUiQuery`,
   `IWeekDayQuery` / `IAppCommand`; register in `HookRegistry.cs`.
7. **Keep changes minimal** — smallest change; no new NuGet packages; no public
   API changes; no unrelated files.
8. **Commit as you go** — one of the three types (`feat`, `fix`, `technical`)
   plus a subject ending in the issue id (`feat: … (timetracker-66k)`),
   enforced by the `commit-msg` hook. Use `technical` for tests, refactoring
   and other purely technical work. Mark a **breaking change** with `!` after
   the type/scope **and** a `BREAKING CHANGE:` footer (the hook requires both
   together) — breaking means something already built, stored or configured
   stops working (plugin contracts, JSON files, options, integration mapping);
   see README.md ("Breaking changes").

   **Stop here** — wait for the user to explicitly advance to phase C.

## C) Implementation review

1. **Run the quality gates** — `dotnet build`, then `dotnet test`, then
   `dotnet format --verify-no-changes`.
2. **Invoke the `architecture-review` agent** (read-only) — it runs the
   architecture tests and returns a findings table against SOLID and the
   CLAUDE.md rules the tests cannot see.
3. **Check the SOLID checklist** — SRP/OCP/LSP/ISP/DIP + code smells, using
   `.claude/skills/solid/references/`.
4. **Fix only your own failures** — read each error, determine whether you
   caused it, fix, re-run; never weaken, delete, or skip tests to make them pass.
5. **Straighten the history** — review the branch's commits and **squash or
   `fixup`** redundant, "wip", or better-told-as-one commits, so the final
   history reads as clean, logical commits.
6. **Record the review results** — unless otherwise specified, write the review
   findings into the issue's **notes** field (`bd update <id>
   --append-notes="…"`) so the outcome survives the session.

   **Stop here** — wait for the user to explicitly advance to phase D.

## D) Finishing

1. **Update issue status** — `bd close <id> --reason="Completed"` for finished
   work; update in-progress items.
2. **Propose follow-up beads** for anything out of scope (create only once the
   user agrees).
3. **Final validation** — re-run build/test/format so "done" is backed by a
   green run.
4. **Merge the branch into master (rebase + no fast-forward)**:

   ```sh
   git checkout timetracker-66k-week-view
   git rebase master            # replay onto the latest master for a clean base
   git checkout master
   git merge --no-ff timetracker-66k-week-view   # explicit merge commit, no fast-forward
   ```

   The `--no-ff` keeps a real merge commit so the feature is a visible,
   revertable unit on `master`.
5. **Ask before pushing** — after the merge, always ask the user whether to
   **push the work (Git and beads)**. Only push with explicit approval:

   ```sh
   git push            # push master (now containing the merged branch)
   bd dolt push        # push the beads issue changes
   ```

   Report the exact commands and any errors; do not push without explicit
   permission.
6. **Hand off** — summarize changes, validation results, issue status, merged
   branch, and any blocked commit/push step.
7. **Note the CI gate** — every push builds & tests on Windows and Linux; the
   manual **Release** workflow publishes both single-file executables.
