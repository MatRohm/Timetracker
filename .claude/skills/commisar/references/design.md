# Design Template

Full template for the design (plan) of a Commisar-filed beads issue. Copy
verbatim and fill in; this is the `--design-file` value (or `--design` for
very short plans). The description stays what + why — see
[description.md](description.md); the scenarios go to the acceptance field —
see [acceptance-criteria.md](acceptance-criteria.md).

The template below is the large-plan shape, distilled from the repo's
richest filed designs (option hints, localization). Scale down by dropping
sections a small issue does not need — but never reorder; the order is
Context first, Verification last.

See: SKILL.md Step 2 (Draft the issue) for where this template goes.

## The template

```markdown
# <Feature name>

Title: `feat: <commit-message-form title> (<issue-id>)`

## Context

<The why in the executor's words: the pain, the rule, the bug. What was
rejected before and why, so the decision is not re-litigated. One short
paragraph.>

## Approach

Ordered steps; build and tests pass after each.

1. **<Step name>** — <exact file paths, symbols, expected behavior. Include
   the concrete anchors a stranger needs: `Path/File.cs:12`, member names,
   test names following `<Method>_When<Condition>_Should<Result>`.>
2. **<Step name>** — ...
3. **<Step name>** — ...
   (Each step independently verifiable; smallest set of touched files.)

## Verification

- `dotnet build` — <expected outcome, e.g. 0 errors>.
- `dotnet test` — <new tests listed by name, existing tests unaffected>.
- `dotnet format --verify-no-changes` — clean.

## Critical files & anchors

- `Path/File.cs` — <what changes there>
- `Path/File.cs:12-40` — <anchor for the read-at-edit-time pointer>

## Assumptions & contingencies

- <What could break, and the pre-made decision of what happens then
  ("switch to X, decision pre-made"). Explicit non-goals restated when they
  matter to the executor.>

## Sequencing

<When the work depends on other issues: which goes first, which files are
touched by both and what each takes. Omit the section when standalone.>
```

## Small-plan shape

For a small task the full shape is overkill; keep the same order, one
prose paragraph per kept section. A rename-size task filed in this repo:

```markdown
Rename `private async void OnClosing(object? sender, WindowClosingEventArgs e)`
to `OnClosingAsync` in Timetracker.App/Views/TrackerWindow.cs and update the
`Closing +=` subscription. No behavior change; the method already ends in
Async for the other renamed handlers (OpenEditorAsync, OnGridKeyDownAsync).

Verification: dotnet build, dotnet test, dotnet format --verify-no-changes.
```

## Rules

- **Never in the description**: the plan goes to `--design` /
  `--design-file` only. The description keeps Goal/Inputs/Non-goals.
- **Steps in dependency order**, each independently verifiable; smallest set
  of touched files.
- **Concrete anchors**: file paths, symbols, line ranges — not "the usual
  place" or "etc.".
- **Load-bearing decisions recorded** with date + reason ("decided X on …
  because …"); rejected alternatives recorded so they are not re-litigated.
- **"Read at edit time" pointers named explicitly**: what to read, at which
  anchor.
- **Non-goals and assumptions listed**: what could break, what happens then.
- Title line inside the design uses git-convention form
  (`feat: … (<issue-id>)`) because it feeds the commit message; the beads
  issue title itself stays unprefixed title case.