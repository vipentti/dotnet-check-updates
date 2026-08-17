# Plan Title

## Summary

Describe the intended outcome in a few sentences. State what will be true when
the work is complete without explaining the full implementation.

## Scope

List the important things that will change and the boundaries that prevent scope
drift. Include exclusions only when they are material.

## Approach

Explain the chosen implementation direction, important interfaces, and global
invariants or compatibility constraints. State each consequential design rule
once; do not duplicate the task list.

## Acceptance Criteria

- State an observable, verifiable finished outcome.
- State another material behavior, negative case, or compatibility expectation.

## Verification

Describe the broad automated and manual checks that establish success: stable
commands or check categories, expected outcomes, external gates, and known
limitations. Group related edge cases instead of duplicating detailed test
matrices in both this file and `tasks.md`.

Strategy only: routine check results stay in the test, review, and CI systems
that already hold them, never in this file or `tasks.md`.

## Risks and Considerations

Include this section only for material compatibility, migration, security, or
delivery risks. Remove it when no such risk needs explicit treatment.
