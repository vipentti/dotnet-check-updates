// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Buffers;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.NuGetUtils;
using DotnetCheckUpdates.Core.ProjectModel;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;

namespace DotnetCheckUpdates.Core;

internal class PackageUpgradeService
{
    private readonly ILogger<PackageUpgradeService> _logger;
    private readonly INuGetService _nugetService;

    public PackageUpgradeService(ILogger<PackageUpgradeService> logger, INuGetService nugetService)
    {
        _logger = logger;
        _nugetService = nugetService;
    }

    public async Task<PackageVersionUpgrade?> GetPackageUpgrade(
        IEnumerable<NuGetFramework> targetFrameworks,
        IPackageReference package,
        UpgradeTarget target,
        CancellationToken cancellationToken = default
    )
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "Searching upgrades for {Name} {CurrentVersion}",
                package.Name,
                package.Version
            );
        }
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
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "No supported frameworks found for {Package} {CurrentVersion} while searching for version {SearchedVersion}",
                        package.Name,
                        package.Version,
                        version
                    );
                }
                return null;
            }

            var foundSupported = false;

            foreach (var ours in ourTargetFrameworks)
            {
                foreach (var supported in frameworks)
                {
                    if (NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(ours, supported))
                    {
                        foundSupported = true;
                        goto found;
                    }
                }
            }

            found:
            if (foundSupported)
            {
                var newVersion = version.ToVersionRange(package.Version);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(
                        "Upgrade found for {Name} {CurrentVersion} -> {NewVersion}",
                        package.Name,
                        package.Version,
                        newVersion
                    );
                }
                return new PackageVersionUpgrade(package.Name, newVersion);
            }
        }

        return null;
    }
}
