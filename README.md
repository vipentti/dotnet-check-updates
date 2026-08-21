![icon](https://raw.githubusercontent.com/vipentti/dotnet-check-updates/main/icon.png)

# dotnet-check-updates

`dotnet-check-updates` is a command-line tool for .NET that helps developers to check for possible upgrades of NuGet packages in their C# projects.

Inspired by [npm-check-updates](https://github.com/raineorshine/npm-check-updates), this tool aims to streamline the process of keeping your .NET projects up-to-date with the latest package versions.

![example-output](https://raw.githubusercontent.com/vipentti/dotnet-check-updates/main/example-output.png)

- Red = major version upgrade
- Cyan = minor version upgrade
- Green = patch version upgrade

## Installation

To install `dotnet-check-updates`, you can use the following command:

```shell
dotnet tool install --global dotnet-check-updates
```

## Features

- Support for C# projects (.csproj files) with [PackageReferences](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- Support for solutions (.sln files) for discovering projects
- Recursive search for project files with customizable depth
- Option to restore packages after an upgrade
- Listing of existing packages with their current versions
- Support for pre-release versions by specifying upgrade target
- Maintains existing version range notation, i.e.

| Current                                                    | Upgraded                                                   |
| ---------------------------------------------------------- | ---------------------------------------------------------- |
| `<PackageReference Include="semver" Version="2.0.6" />`    | `<PackageReference Include="semver" Version="2.3.0" />`    |
| `<PackageReference Include="semver" Version="[2.0.6,)" />` | `<PackageReference Include="semver" Version="[2.3.0,)" />` |
| `<PackageReference Include="semver" Version="[2.0.6]" />`  | `<PackageReference Include="semver" Version="[2.3.0]" />`  |

## Limitations

- Supports upgrading packages using [PackageReferences](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) in project and `Directory.Build.props` files
- Supports upgrading packages using [PackageVersion](https://devblogs.microsoft.com/nuget/introducing-central-package-management/) in `Directory.Packages.props` files
- Only certain [Version ranges](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#version-ranges) are supported when checking for upgrades (pre-releases are supported for these ranges):

| Notation | Description                |
| -------- | -------------------------- |
| `1.0`    | Minimum version, inclusive |
| `[1.0,)` | Minimum version, inclusive |
| `[1.0]`  | Exact version match        |

## Project disovery

By default, dotnet-check-updates will search the current directory for C# project (.csproj) files.
If no project files are found, it will then search for solution (.sln) files in the current directory.

## Usage

To use dotnet-check-updates, run the following command in your terminal:

```shell
dotnet-check-updates [OPTIONS]
```

NOTE: Alternatively when using the global tool you may also invoke:

```shell
dotnet check-updates [OPTIONS]
```

See [Invoke a global tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools#invoke-a-global-tool) for more information.

### Options

To list all available options run:

```shell
dotnet-check-updates --help
```

### Examples

Check for the latest versions for all dependencies in discovered projects:

```shell
dotnet-check-updates
```

List existing packages and their versions while also searching for upgrades:

```shell
dotnet-check-updates --list
```

Include filters:

```shell
# Include only packages which contain the word system (case-insensitive) in their package name
dotnet-check-updates --filter 'System'
# or
dotnet-check-updates --filter '*System*'

# Include only packages which start with the word System
dotnet-check-updates --filter 'System*'
```

Exclude filters:

```shell
# Exclude packages which contain the word system (case-insensitive) in their package name
dotnet-check-updates --exclude 'System'
# or
dotnet-check-updates --exclude '*System*'

# Exclude packages which start with the word System
dotnet-check-updates --exclude 'System*'
```

Upgrade packages in the found projects to the latest version:

```shell
dotnet-check-updates --upgrade
```

> **Make sure your projects are in version control and all changes have been committed. This _will_ overwrite your project files.**

Search for projects in a specific solution file, upgrade packages and restore them:

```shell
dotnet-check-updates --solution path/to/your/solution.sln --upgrade --restore
```

> **Make sure your projects are in version control and all changes have been committed. This _will_ overwrite your project files.**

## JSON output

For automation, use bare `--json` with non-interactive checks and upgrades:

```shell
dotnet-check-updates --json
```

- `--json` is non-interactive only; it cannot be combined with `--interactive` or `--version`.
- Only bare `--json` is accepted. Forms such as `--json=<value>`, `--json:<value>`, `--json true`, and `--json false` are rejected with a diagnostic on stderr before argument parsing, regardless of placement before `--`.
- `--help-dump-opencli` is recognized case-insensitively as the first argument; in that mode OpenCLI output is preserved and `--json` does not affect routing.
- Successful execution writes a single UTF-8 JSON document to stdout. Progress bars, trees, upgrade guidance, optional logging (`DCU_ENABLE_LOGGING`), and restore subprocess output are suppressed from stdout and, when present, appear on stderr. Failed execution writes no partial JSON to stdout and preserves the existing non-zero status.
- File mutations are non-atomic: files are saved sequentially before restore. A later save or restore failure can leave earlier mutations applied while still returning non-zero without a JSON document.

### Schema version 1

A [JSON Schema document](schemas/json-output-v1.schema.json) is available for validating output and generating consumer types. It uses JSON Schema Draft 2020-12, is included in the NuGet package at `schemas/json-output-v1.schema.json`, and allows unknown properties so compatible schema version 1 additions remain valid. The CLI does not add a `$schema` property to output; consumers can associate the schema without changing the output contract.

```json
{
  "schemaVersion": 1,
  "upgradeRequested": true,
  "upgradesApplied": true,
  "solutions": [
    {
      "path": "Example.sln",
      "projects": ["src/App/App.csproj"]
    }
  ],
  "checkedFiles": [
    {
      "path": "Directory.Packages.props",
      "kind": "directoryPackagesProps",
      "packageCount": 1,
      "targetFrameworks": ["net8.0", "net9.0"],
      "packages": [
        {
          "name": "Example",
          "currentVersion": "[1.0.0,)",
          "targetVersion": "[2.0.0,)",
          "upgradeType": "major",
          "applicableFrameworks": ["net8.0"]
        }
      ]
    }
  ]
}
```

| Property | Type | Notes |
| --- | --- | --- |
| `schemaVersion` | integer | Always `1`. |
| `upgradeRequested` | boolean | True when `--upgrade` was supplied. |
| `upgradesApplied` | boolean | True only when at least one checked file was written with an upgraded package. |
| `solutions[].path` | string | Normalized JSON solution path. |
| `solutions[].projects` | string[] | Normalized supported `.csproj`/`.fsproj` members from discovery; props files excluded. |
| `checkedFiles[].path` | string | Normalized checked-file path. |
| `checkedFiles[].kind` | string | `project`, `directoryBuildProps`, or `directoryPackagesProps`. |
| `checkedFiles[].packageCount` | integer | Filtered package-reference count (independent of `packages` length). `--show-package-count` is accepted in JSON mode but has no extra effect. |
| `checkedFiles[].targetFrameworks` | string[] | Effective frameworks (`net8.0`, `netstandard2.1`, etc.). |
| `checkedFiles[].packages[].name` | string | Package ID as read. |
| `checkedFiles[].packages[].currentVersion` | string or null | Pre-upgrade version string; null when versionless. |
| `checkedFiles[].packages[].targetVersion` | string or null | Proposed/written version; null when no upgrade. |
| `checkedFiles[].packages[].upgradeType` | string or null | Null when `targetVersion` is null; otherwise `none`, `major`, `minor`, `patch`, `release`. |
| `checkedFiles[].packages[].applicableFrameworks` | string[] | Frameworks for which the reference was evaluated. |

- All shown properties are required. Arrays are always present (`[]` when empty). Nullable package properties are `null` when absent, not omitted.
- `--show-absolute` controls absolute vs relative paths: with it, emit canonical full paths; otherwise emit `Path.GetRelativePath(canonicalCwd, canonicalFullPath)` using platform-native separators.
- Relative `--project`/`--solution` are resolved against the process working directory even when `--cwd` differs; JSON identifies the file actually read or mutated.
- Default `packages` contains only references with a non-null `targetVersion`; with `--list` every filtered reference is included. Conditioned duplicates remain separate entries; exact duplicates are retained.
- Deterministic order: `solutions` by path, `solutions[].projects` by path, `checkedFiles` by path, frameworks by string, `packages` by name (case-insensitive then ordinal) then `currentVersion`/`targetVersion`/`upgradeType` (null first) then joined sorted `applicableFrameworks`, stable on ties. `targetVersion`/`upgradeType` use existing `PackageReference.GetVersionString()` normalized form and `UpgradeType` comparison.
- Version 1 consumers must ignore unknown properties; compatible additions may add properties within `schemaVersion` 1.

## License

dotnet-check-updates is licensed under the [MIT License](https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md)
