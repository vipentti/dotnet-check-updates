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

- Currently only supports upgrading packages using [PackageReferences](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) in project files
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
dotnet-check-updates --include 'System'
# or
dotnet-check-updates --include '*System*'

# Include only packages which start with the word System
dotnet-check-updates --include 'System*'
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

## License

dotnet-check-updates is licensed under the [MIT License](https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md)
