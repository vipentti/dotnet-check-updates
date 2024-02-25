# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Breaking Changes
- Default alias for `-i|--include` is changed to `-f|--filter`\
  Additionally `-I (capital i)`, `--include` and `--inc` can be used.
- Default alias for `-e|--exclude` is changed to `-x|--exclude`\
  Additionally `-E (capital e)`, `--reject` and `--exc` can be used.

### Added
- Support for `-i|--interactive` to choose packages to upgrade interactively
- Support `--page-size` to determine number of items shown in interactive mode
- Support for V2 NuGet feeds
- Support for `nuget.config` package sources
- Support for `--no-progress` to hide progress bar while fetching package information
- Support for `--nuget-source` to specify NuGet package sources

### Changed
- Handling when no packages match provided filters (`--include` or `--exclude`)

## [0.2.4] / 2024-02-16
- Add support for showing package count with --show-package-count

## [0.2.3] / 2024-02-11
- Implement support for Directory.Build.props files
- Implement -i|--include and -e|--exclude for filtering specific packages to include or exclude

## [0.2.2] / 2023-11-22
- Implement support for F# projects (.fsproj)

## [0.2.1] / 2023-11-22
- Normalize package ids using ToLowerInvariant before calling NuGet api

## [0.2.0] / 2023-11-15
- Add .NET 8 support

## [0.1.0] / 2023-11-10
- Initial release

[Unreleased]: https://github.com/vipentti/dotnet-check-updates/compare/0.2.4...HEAD
[0.2.4]: https://github.com/vipentti/dotnet-check-updates/compare/0.2.3...0.2.4
[0.2.3]: https://github.com/vipentti/dotnet-check-updates/compare/0.2.2...0.2.3
[0.2.2]: https://github.com/vipentti/dotnet-check-updates/compare/0.2.1...0.2.2
[0.2.1]: https://github.com/vipentti/dotnet-check-updates/compare/0.2.0...0.2.1
[0.2.0]: https://github.com/vipentti/dotnet-check-updates/compare/0.1.0...0.2.0
[0.1.0]: https://github.com/vipentti/dotnet-check-updates/tree/0.1.0
