# Add JSON Output for Automated Consumers

## Summary

Add `--json` output for non-interactive package checks and upgrades. JSON mode provides a stable, versioned document suitable for automated systems and agents while keeping stdout free from human-oriented output.

## Scope

- Add `--json` to non-interactive check and upgrade workflows.
- Report discovered solutions, projects, package versions, available upgrades, framework context, and whether upgrades were applied.
- Preserve existing filtering, targeting, listing, path formatting, upgrade, and restore behavior.
- Document JSON usage and contract.
- Reject `--json` with `--interactive` or `--version`.
- Exclude new exit-code semantics for available updates and additional output formats.

## Approach

Use a dedicated JSON result model rather than serializing internal project models. Emit one document through `System.Text.Json` after non-interactive processing completes successfully.

Top-level contract:

- `schemaVersion`: integer, initially `1`.
- `upgradesApplied`: boolean reflecting `--upgrade`.
- `solutions`: array containing each solution path and its project paths.
- `projects`: array containing each project path, target frameworks, and selected packages.

Each package contains:

- `name`
- `currentVersion`, nullable when no usable version exists
- `targetVersion`, null when no upgrade is available
- `upgradeType`, null when no upgrade is available
- `conditions`, preserving conditioned package-reference identity through group, operator, target framework, and raw condition

Default JSON output includes packages with available upgrades. `--list` also includes unchanged packages, matching existing text-mode intent. Existing include and exclude filters apply before serialization.

Existing `--show-absolute` behavior controls emitted paths. Output ordering must be deterministic for solutions, projects, packages, and conditions.

JSON mode suppresses progress rendering, trees, upgrade guidance, restore command output, and logging from stdout. Successful stdout contains only the JSON document. Diagnostics and failures use stderr with existing nonzero failure behavior. Failed execution must not emit a partial JSON document.

Text output remains unchanged when `--json` is absent.

## Acceptance Criteria

- `--json` produces valid schema version 1 JSON for project and solution discovery.
- Check results identify current versions, available target versions, upgrade types, target frameworks, and conditioned package identities.
- `--list`, filters, target selection, relative or absolute paths, `--upgrade`, and `--restore` retain their existing semantics.
- JSON upgrades modify project files as before and report `upgradesApplied: true`.
- Successful JSON-mode stdout contains no ANSI markup, progress output, guidance, logs, or subprocess output.
- Invalid combinations with `--interactive` or `--version` fail validation.
- Errors produce no partial JSON stdout and retain nonzero exit status.
- Existing non-JSON output and behavior remain compatible.
- README and command help describe `--json`, compatibility constraints, and schema.

## Verification

Add focused command and serialization tests covering project and solution output, available and unchanged packages, conditioned duplicate package references, filtering, path modes, upgrade application, empty results, deterministic ordering, incompatible options, and clean stdout.

Run repository test suite and lint or formatting checks used by pull-request CI. Exercise packaged CLI output to parse stdout as JSON and confirm text mode remains unchanged.

## Risks and Considerations

Schema version 1 becomes a public automation contract. Dedicated output types prevent accidental contract changes when internal models evolve.

Restore subprocesses and optional logging can corrupt stdout unless explicitly redirected or suppressed in JSON mode. Tests must cover stdout isolation.
