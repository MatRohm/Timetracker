# Description Template

Full template for a Commisar-filed beads issue description. Copy verbatim and
fill in; do not abbreviate or reorder the sections. This is the
`--description` value; the scenarios go to the acceptance field — see
[acceptance-criteria.md](acceptance-criteria.md); the plan goes to the
design field — see [design.md](design.md).

See: SKILL.md Step 2 (Draft the issue) for where this template goes.

## The template

Opens with these sections at the top, in this order — no section may be
missing. The `Inputs` section is omitted (not left empty) when the feature
takes no UI input; that omission is implied by the non-goals.

```markdown
# Goal

<What needs to be achieved, one or two sentences of observable end-state.
The reader learns within seconds what "done" means.>

# Inputs

| Input       | Type             | Validation rules                  |
| ----------- | ---------------- | --------------------------------- |
| <name>      | <type>           | <rules>                           |

<Include one row per UI input. Type is a concrete kind: string,
alphabetical string, positive integer, decimal, path … Write "everything
is allowed" under Validation rules when none apply. Never omit the type
column: an unallowed-but-unvalidated input is the classic grot finding.>

# Non-goals

- <Deliberately out of scope, so scope disputes die before they start.>
```

## Filled example

```markdown
# Goal

The week view shows each day's entries as an indented tree under a parent
entry, so users can read nested time at a glance.

# Inputs

| Input        | Type             | Validation rules                  |
| ------------ | ---------------- | --------------------------------- |
| Indent depth | Positive integer | 1–8; rejects 0, negatives, text   |

# Non-goals

- No editing of entries inside the tree in this iteration.
- No persistence of the indent depth across sessions.
```