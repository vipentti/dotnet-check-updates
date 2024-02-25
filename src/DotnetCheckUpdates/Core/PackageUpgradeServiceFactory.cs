// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.NuGetUtils;
using Microsoft.Extensions.Logging;

namespace DotnetCheckUpdates.Core;

internal class PackageUpgradeServiceFactory(
    ILoggerFactory loggerFactory,
    ICurrentDirectory currentDirectory,
    NuGetConfigurationPackageSourceProvider configurationPackageSourceProvider,
    NuGetServiceFactory nuGetServiceFactory
) : IPackageUpgradeServiceFactory
{
    public PackageUpgradeService GetPackageUpgradeService()
    {
        var logger = loggerFactory.CreateLogger<PackageUpgradeService>();

        configurationPackageSourceProvider.Root = currentDirectory.CurrentDirectory;

        var source = new MultiSourceNuGetService(
            loggerFactory.CreateLogger<MultiSourceNuGetService>(),
            configurationPackageSourceProvider,
            nuGetServiceFactory
        );

        return new(logger, source);
    }
}
