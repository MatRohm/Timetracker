# End-of-session retain pass

How the agents use **Hindsight**, the project's long-term memory. This file is
the single source of truth: `.claude/CLAUDE.md` (Memory) and the `development`,
`commisar`, `lictor` and `grotz` skills all reference it.

## The pass

Run it at session close — and, for those four skills, at the end of the skill's
own workflow — or whenever the user asks:

1. **Sweep** the session for candidates: decisions, rules the user stated,
   non-obvious configuration facts, corrections.
2. **Filter.** Keep only what is *persistent* (still relevant in a week),
   *non-obvious* (not derivable from the code or config), *not already stored*
   (check with `recall`) and *consequential*. Feature mechanics belong in the
   beads issue/design, not in memory; one-off session details stay out.
3. **Retain every decision with its background.** A decision memory carries what
   was decided, why, which alternatives were rejected and why, the date, the
   source (boss directive or agent) and the scope. **A decision without its
   reasons is not retained.**
4. **Supersede, don't duplicate.** `recall` related memories first; when a new
   fact revokes an older one, retain the new fact naming the replaced memory and
   invalidate the old one (`invalidate_memory`).
5. **Retain, then report.** Store the clear keepers automatically, then report
   the batch so the user can correct or invalidate anything wrong.
6. **Fallback.** If Hindsight is unreachable, store the same content with
   `bd remember` and mirror it later — Hindsight has no offline queue.

Memory work is **not project work**: it is never tracked in beads (no issue, no
commit) and it changes no repository file. That is why it does not conflict with
the read-only rules of the `lictor` and `grotz` skills.

## The decision record

One memory per decision, shaped as:

    <Decision> (decided <date>, <source: boss directive | agent decision>, <issue/commit>):
    because <reasons>; rejected <alternative> because <why>; applies to <scope>.

Example:

    TimeBooking plugin (2026-10-10, boss decisions, timetracker-l6h): book tracked
    time by running a user-supplied Python script as `<interpreter> <script.py>`
    with the range's entries as a JSON array on stdin; success = exit code 0.
    Because the company time tool exposes no REST API. Rejected: a REST
    integration (no API); per-day buttons in the week view (would couple
    plugins); skipping already-booked entries (the boss chose warn-only).

The rejected-alternative clause is what stops a later session from re-proposing
a path already closed.

## When each skill runs it

| Skill | Trigger |
|---|---|
| `development` | end of phase D (finishing), before the hand-off |
| `commisar` | after filing or reviewing, after the report |
| `lictor` | after the result report |
| `grotz` | after the report to the boss (the report is unchanged) |
