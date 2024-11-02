// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Buffers;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.NuGetUtils;
using DotnetCheckUpdates.Core.ProjectModel;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core;

internal partial class PackageUpgradeService
{
    private readonly ILogger<PackageUpgradeService> _logger;
    private readonly INuGetService _nugetService;

    public PackageUpgradeService(ILogger<PackageUpgradeService> logger, INuGetService nugetService)
    {
        _logger = logger;
        _nugetService = nugetService;
    }

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Searching upgrades for {Name} {CurrentVersion}"
    )]
    public static partial void LogSearchingForUpgrades(
        ILogger logger,
        string name,
        VersionRange currentVersion
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No supported frameworks found for {Package} {CurrentVersion} while searching for version {SearchedVersion}"
    )]
    public static partial void LogNoSupportedFrameworks(
        ILogger logger,
        string package,
        VersionRange currentVersion,
        NuGetVersion searchedVersion
    );

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Upgrade found for {Name} {CurrentVersion} -> {NewVersion}"
    )]
    public static partial void LogUpgradeFound(
        ILogger logger,
        string name,
        VersionRange currentVersion,
        VersionRange newVersion
    );

    public async Task<PackageVersionUpgrade?> GetPackageUpgrade(
        IEnumerable<NuGetFramework> targetFrameworks,
        IPackageReference package,
        UpgradeTarget target,
        CancellationToken cancellationToken = default
    )
    {
        if (!package.HasVersion)
        {
            return null;
        }

        LogSearchingForUpgrades(_logger, package.Name, package.Version);

        var ourTargetFrameworks = targetFrameworks.ToImmutableArray();

        // Versions goes from oldest to latest
        var versions = (
            await _nugetService.GetPackageVersionsAsync(package.Name, cancellationToken)
        )
            .OrderBy(it => it)
            .ToArray();

        for (var i = versions.Length - 1; i >= 0; --i)
        {
            var version = versions[i];

            if (!package.Version.NewVersionSatisfiesTargetAndRange(version, target))
            {
                continue;
            }

            var frameworks = await _nugetService.GetSupportedFrameworksAsync(
                package.Name,
                version.ToString(),
                cancellationToken
            );

            if (frameworks.Count == 0)
            {
                LogNoSupportedFrameworks(_logger, package.Name, package.Version, version);

                return null;
            }

            var foundSupported = false;

            foreach (var ours in ourTargetFrameworks)
            {
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions
                foreach (var supported in frameworks)
                {
                    if (NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(ours, supported))
                    {
                        foundSupported = true;
#pragma warning disable S907 // "goto" statement should not be used
                        goto found;
#pragma warning restore S907 // "goto" statement should not be used
                    }
                }
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions
            }

            found:
            if (foundSupported)
            {
                var newVersion = version.ToVersionRange(package.Version);
                LogUpgradeFound(_logger, package.Name, package.Version, newVersion);
                return new PackageVersionUpgrade(package.Name, newVersion);
            }
        }

        return null;
    }
}
