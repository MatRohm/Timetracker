# Acceptance Criteria Template

Full template for the acceptance scenarios of a Commisar-filed beads issue.
Copy verbatim and fill in; one scenario per observable behavior. This is the
beads **acceptance field** (`--acceptance`) value, not the description; the
description template lives in [description.md](description.md).

## The template

Write the list of conditions that make the feature done in
Cucumber/Gherkin style, one `Scenario` per observable behavior. Cover every
validation rule from the Inputs table: valid and rejected inputs both get a
scenario.

```gherkin
Scenario: <one observable behavior>
    Given <initial state>
    When <action>
    Then <expected outcome>
```

## Renderer formatting rule

Format for bd's terminal renderer: plain paragraphs are re-flowed by
`bd show` (lipgloss), which **strips line-leading indents**. **Wrap each
scenario in its own fenced ```-block** so Given/When/Then line breaks and
indentation survive (`bd show` preserves relative indents inside fences;
verified against bd 1.3.1). The whole value is passed as one
`--acceptance="<value>"` argument; fences inside it are quoted, not
escaped.

## Multiple scenarios, fenced per scenario

```text
--acceptance="
​```
Scenario: Valid depth renders the tree
    Given the settings dialog is open
    When I set indent depth to 4
    Then the week view renders entries as an indented tree to depth 4
​```

​```
Scenario: Zero depth is rejected
    Given the settings dialog is open
    When I set indent depth to 0
    Then a validation message rejects the value and the tree keeps the previous depth
​```
"
```

(The zero-width space before each fence in the example above only keeps
this reference file from breaking its own fence; do not copy it — use a
plain ```` ``` ```` fence when filing.)