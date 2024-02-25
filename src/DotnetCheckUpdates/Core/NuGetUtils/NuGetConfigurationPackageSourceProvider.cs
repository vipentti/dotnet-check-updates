// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Configuration;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal partial class NuGetConfigurationPackageSourceProvider(
    ILogger<NuGetConfigurationPackageSourceProvider> logger,
    NuGetSettingsProvider nuGetSettings
) : INuGetPackageSourceProvider
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading default settings from {Root}")]
    private static partial void LogLoadingDefaultSettings(ILogger logger, string root);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Found NuGet configurations: {@ConfigFilePaths}"
    )]
    private static partial void LogFoundConfigurations(
        ILogger logger,
        IEnumerable<string> configFilePaths
    );

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Enabled package source {Name} {SourceUri} {ProtocolVersion}"
    )]
    private static partial void LogEnabledPackageSource(
        ILogger logger,
        string name,
        Uri sourceUri,
        int protocolVersion
    );

    public IEnumerable<PackageSource> GetPackageSources()
    {
        var packageProvider = new PackageSourceProvider(nuGetSettings.NuGetSettings);

        foreach (var item in packageProvider.LoadPackageSources())
        {
            if (item.IsEnabled && item.SourceUri is not null)
            {
                LogEnabledPackageSource(logger, item.Name, item.SourceUri, item.ProtocolVersion);

                yield return item;

                // https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file#packagesources
                // Add json sources additionally with v3 unless they already are
                if (
                    item.ProtocolVersion != 3
                    && item.Source.EndsWith(
                        CliConstants.JsonExtensionWithDot,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var itemClone = item.Clone();
                    itemClone.ProtocolVersion = 3;
                    LogEnabledPackageSource(
                        logger,
                        itemClone.Name,
                        itemClone.SourceUri,
                        itemClone.ProtocolVersion
                    );
                    yield return itemClone;
                }
            }
        }
    }
}
