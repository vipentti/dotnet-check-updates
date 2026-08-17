# Add JSON Output for Automated Consumers

## Summary

Add `--json` output for non-interactive package checks and upgrades. JSON mode provides a stable, versioned document suitable for automated systems and agents while keeping stdout free from human-oriented output.

## Scope

- Add `--json` to non-interactive check and upgrade workflows.
- Report discovered solutions, checked files, package versions, available upgrades, applicable frameworks, and applied mutations.
- Distinguish project files from `Directory.Build.props` and `Directory.Packages.props`.
- Preserve existing non-JSON discovery occurrences, path values, rendering, progress, mutation, filtering, targeting, listing, upgrade, and restore behavior.
- Document JSON usage and version 1 contract.
- Reject `--json` with `--interactive` or `--version`.
- Exclude new exit-code semantics for available updates and additional output formats.

## Approach

Use dedicated JSON output types rather than serializing internal project models. Emit one UTF-8 JSON document through `System.Text.Json` after non-interactive processing completes successfully. Property names and string tokens use exact casing shown below. Whitespace is not contractual.

Separate upgrade detection from rendering through an internal check result built with existing upgrade comparison helpers. Retain original discovery occurrence and display-path values alongside canonical JSON identity and pre-upgrade and target package state. Text rendering and mutation consume original ordered occurrences exactly as before. JSON projection and JSON-mode mutation consume one result per canonical identity. This avoids renderer-owned upgrade state without changing text-mode duplicate processing, progress, saves, or output.

### Version 1 shape

```json
{
  "schemaVersion": 1,
  "upgradeRequested": true,
  "upgradesApplied": true,
  "solutions": [
    {
      "path": "Example.sln",
      "projects": [
        "src/App/App.csproj"
      ]
    }
  ],
  "checkedFiles": [
    {
      "path": "Directory.Packages.props",
      "kind": "directoryPackagesProps",
      "packageCount": 1,
      "targetFrameworks": [
        "net8.0",
        "net9.0"
      ],
      "packages": [
        {
          "name": "Example",
          "currentVersion": "[1.0.0,)",
          "targetVersion": "[2.0.0,)",
          "upgradeType": "major",
          "applicableFrameworks": [
            "net8.0"
          ]
        }
      ]
    },
    {
      "path": "src/App/App.csproj",
      "kind": "project",
      "packageCount": 0,
      "targetFrameworks": [
        "net8.0"
      ],
      "packages": []
    }
  ]
}
```

All properties shown above are required. Arrays are always present and use `[]` when empty. Nullable package properties are emitted as JSON `null`, not omitted. Version 1 consumers must ignore unknown properties. Unknown extra properties may therefore be added compatibly within schema version 1; changes to existing property names, types, tokens, nullability, semantics, or ordering rules require a new `schemaVersion`.

### Top-level fields

| Property | Type | Semantics |
| --- | --- | --- |
| `schemaVersion` | integer | Exact value `1`. |
| `upgradeRequested` | boolean | True when `--upgrade` was supplied, regardless of whether an upgrade exists. |
| `upgradesApplied` | boolean | True only when at least one checked file was successfully written with an upgraded package. |
| `solutions` | array | Discovered solution files and supported project members returned by current discovery. |
| `checkedFiles` | array | Every file checked for package versions, including projects and supported props files. |

### Solution fields

| Property | Type | Semantics |
| --- | --- | --- |
| `path` | string | Normalized JSON solution path using the path rules below. |
| `projects` | string array | Normalized JSON paths for supported `.csproj` and `.fsproj` members returned by current discovery. Unsupported project types and inferred props files are excluded. |

### Checked-file fields

| Property | Type | Semantics |
| --- | --- | --- |
| `path` | string | Normalized JSON checked-file path using the path rules below. |
| `kind` | string | One of `project`, `directoryBuildProps`, or `directoryPackagesProps`. |
| `packageCount` | integer | Count of all parsed package references remaining after include and exclude filtering. |
| `targetFrameworks` | string array | Effective frameworks used by the checker, including inferred frameworks for files without declarations. |
| `packages` | array | Selected package-reference results owned by this file. |

`kind` is `directoryBuildProps` for `Directory.Build.props`, `directoryPackagesProps` for `Directory.Packages.props`, and `project` for other checked project files.

`packageCount` is independent of `packages` array length because default JSON output omits unchanged references. `--show-package-count` remains accepted in JSON mode but has no additional effect because `packageCount` is always present.

In JSON mode, each canonical full-path identity appears once in `checkedFiles`. Use ordinal-ignore-case comparison on Windows and ordinal comparison on other platforms, and process each resulting unique identity once. For every identity group, choose the ordinal-smallest exact canonical full-path string as its representative. Use that same representative for `checkedFiles[].path` and every corresponding `solutions[].projects` entry, then remove repeated representative entries within each solution before sorting. This guarantees exact path joins between solution membership and checked files, including Windows case variants. This identity does not resolve symlinks, junctions, hard links, or filesystem-specific case behavior beyond that comparison rule.

### Package fields

| Property | Type | Semantics |
| --- | --- | --- |
| `name` | string | NuGet package ID as read from the checked file. |
| `currentVersion` | string or null | Pre-upgrade version or supported version range. Null when the reference has no usable version. |
| `targetVersion` | string or null | Proposed persisted version or range. Null when no upgrade is available. |
| `upgradeType` | string or null | Null when `targetVersion` is null. Otherwise one of `none`, `major`, `minor`, `patch`, or `release`. |
| `applicableFrameworks` | string array | Effective target frameworks for which the checker evaluated this package reference. |

Version strings use the same normalized NuGet representation returned by `PackageReference.GetVersionString()` and written during upgrade. Supported bracket notation is retained without spaces, for example `1.0.0`, `[1.0.0]`, or `[1.0.0,)`. `currentVersion` always describes pre-upgrade state. `targetVersion` describes the proposed or written state.

Framework strings use `NuGetFramework.GetShortFolderName()`, for example `net8.0` or `netstandard2.1`.

`applicableFrameworks` intentionally exposes evaluated framework applicability, not parsed MSBuild condition syntax. Current parsing recognizes supported target-framework predicates only. Unsupported conditions, including configuration-only conditions, are not represented or claimed to be preserved by JSON output.

Default JSON output includes package references with a non-null `targetVersion`. `--list` includes every parsed package reference remaining after include and exclude filtering, including unchanged and versionless references. Separate conditioned references remain separate array entries even when package names match. Exact duplicate entries are retained.

### Paths and ordering

In JSON mode, establish canonical full paths before validation, discovery, or file processing:

1. Normalize process working directory with `Path.GetFullPath`.
2. Resolve effective `--cwd` against process working directory, matching current command semantics.
3. Resolve explicit relative `--project` and `--solution` selectors against process working directory, also matching current semantics, regardless of effective `--cwd`.
4. Pass canonical selector paths into validation and discovery. Discovery must return canonical full paths for automatically found solutions, supported solution members, checked projects, and props files, resolving paths against the discovery root or containing solution as appropriate.
5. Group checked files by canonical identity, choose the representative path defined above, and rewrite supported solution membership to that representative.
6. Carry representative values through JSON-mode reads, identity comparison, package checking, saves, restore, solution membership, and JSON projection. JSON projection must not reinterpret discovered relative paths.

With `--show-absolute`, emit each canonical full path. Without it, emit `Path.GetRelativePath(canonicalCwd, canonicalFullPath)`. JSON paths use platform-native directory separators.

Without `--json`, preserve original discovered path values and processing occurrences through validation, reads, package checking, rendering, saves, and restore. Text mode continues using its existing formatter and must not receive canonical JSON display paths.

Arrays use these deterministic orders:

1. `solutions`: `path`, ordinal.
2. `solutions[].projects`: path, ordinal.
3. `checkedFiles`: `path`, ordinal.
4. `targetFrameworks` and `applicableFrameworks`: framework string, ordinal.
5. `packages`: `name` ordinal-ignore-case, then `name` ordinal, `currentVersion` ordinal with null first, `targetVersion` ordinal with null first, `upgradeType` ordinal with null first, then joined sorted `applicableFrameworks` ordinal.
6. Package entries equal on every public sort key retain source-file order.

### Output and failure behavior

Use one pre-parse argument classifier matching Spectre.Console.Cli global-token behavior:

1. If first argument is `--help-dump-opencli`, select OpenCLI mode immediately. Preserve framework OpenCLI output and status; ignore later `--json` for routing because Spectre replaces the argument list for this shortcut.
2. Otherwise, inspect only arguments before the first exact `--` end-of-options marker.
3. If that option slice contains `-?`, `-h`, or `--help`, select help mode. Help takes precedence over `--json`, emits normal human-readable help to stdout with status 0, and does not enter JSON mode.
4. Otherwise, if that option slice contains exact `--json`, select JSON mode.
5. Otherwise, select default mode. Tokens at or after `--` never select help or JSON routing and remain for Spectre to handle.

JSON mode is therefore known before command parsing, so process-level routing also covers parse failures. JSON mode suppresses progress rendering, trees, upgrade guidance, and logging from stdout. In JSON mode, both restore subprocess stdout and stderr are forwarded to process stderr, preserving normal restore chatter and failure diagnostics without contaminating stdout. Successful stdout contains only the JSON document. Other diagnostics and failures also use stderr with existing nonzero failure behavior. Failed execution emits no partial JSON document.

Upgrade and restore execution remains non-atomic. Files are saved sequentially before restore. A later save or restore failure can leave earlier file mutations applied while returning nonzero and emitting no JSON document. Absence of successful JSON therefore does not guarantee absence of filesystem changes.

Text output remains unchanged when `--json` is absent.

## Acceptance Criteria

- `--json` produces the exact schema version 1 shape and representations defined above.
- Solutions contain only supported `.csproj` and `.fsproj` members returned by current discovery; every unique canonical full-path identity for a checked project or supported props file appears once in `checkedFiles` with correct `kind`.
- In JSON mode, shared project path identities retain membership in every discovered solution without duplicate checked-file results or processing, and every membership path exactly matches its checked-file representative.
- Every checked file reports filtered `packageCount`; `--show-package-count` is accepted and inert in JSON mode.
- Results preserve pre-upgrade versions and identify target versions, upgrade types, effective frameworks, and whether mutation was requested or applied.
- `--upgrade` with no applicable upgrades reports `upgradeRequested: true` and `upgradesApplied: false`.
- `--list`, filters, target selection, `--upgrade`, and `--restore` retain existing semantics; JSON-mode processing and paths follow the canonicalization rules above.
- Relative explicit project and solution selectors retain process-working-directory semantics even when `--cwd` differs, and emitted JSON identifies the file actually read or mutated.
- Without `--json`, original discovery occurrences, path values, processing, progress, saves, and rendered output remain unchanged.
- Pre-parse classification handles `-?`, `-h`, `--help`, `--`, `--json`, and first-argument `--help-dump-opencli` exactly as defined above.
- Successful JSON-mode stdout contains no ANSI markup, progress output, guidance, logs, or subprocess output; restore output and diagnostics remain available on stderr.
- Invalid combinations with `--interactive` or `--version` fail validation.
- Errors produce no partial JSON stdout, retain nonzero exit status, and preserve documented non-atomic mutation behavior.
- Existing non-JSON output and behavior remain compatible.
- README and command help describe invocation, schema, field semantics, compatibility constraints, and failure behavior.

## Verification

Add focused command and serialization tests covering canonical schema output, exact enum tokens, project and solution discovery, supported versus unsupported solution member types, overlapping solution membership with unique JSON identities and unchanged text-mode duplicate occurrences, Windows case-variant membership using exact representative checked-file paths, checked-file kinds and filtered package counts, `--show-package-count`, available and unchanged packages, conditioned duplicates, unsupported non-framework conditions, version ranges, inferred frameworks, filters, relative explicit project and solution selectors with a different `--cwd`, absolute and relative JSON paths including outside-cwd paths and paths containing cwd text, unchanged text display paths, deterministic ordering, empty arrays and nulls, upgrades with and without mutations, restore and save failures including restore diagnostics on stderr, incompatible options, and stdout isolation.

Run repository test suite and lint or formatting checks used by pull-request CI. Packaged CLI verification must cover all three help aliases with `--json`, both relevant argument orderings, help and JSON tokens after `--`, first-argument `--help-dump-opencli` followed by `--json`, successful JSON execution, JSON command parse failure such as `--json --unknown-option`, JSON validation failure, JSON runtime failure, and successful JSON execution with `DCU_ENABLE_LOGGING=1`. Help cases must produce human-readable stdout and status 0; OpenCLI precedence must preserve framework output without JSON routing. JSON execution success cases must parse exact JSON-only stdout. Every JSON-mode failure case must produce empty stdout, diagnostics on stderr, and nonzero status; default-mode boundary cases preserve existing framework stream behavior. Confirm text mode remains unchanged.

## Risks and Considerations

Schema version 1 becomes a public automation contract. Dedicated output types and contract tests prevent internal model or serializer defaults from changing it accidentally.

Restore subprocesses and optional logging can corrupt stdout unless explicitly redirected in JSON mode. Upgrade failures can leave mutations without a success document because existing writes are non-atomic.
