// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Configuration;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal partial class NuGetPackageSourceProvider(
    ILogger? logger,
    IEnumerable<PackageSource>? sources = default
) : INuGetPackageSourceProvider
{
    private static readonly IEnumerable<PackageSource> s_defaultSources =
    [
        new PackageSource(NuGetConstants.V3FeedUrl) { ProtocolVersion = 3 },
    ];

    private readonly ImmutableArray<PackageSource> _providedSources =
        sources?.ToImmutableArray() ?? ImmutableArray<PackageSource>.Empty;

    public IEnumerable<PackageSource> GetPackageSources()
    {
        var sourcesToUse = _providedSources.IsEmpty ? s_defaultSources : _providedSources;

        foreach (var item in sourcesToUse)
        {
            if (logger is not null)
            {
                LogEnabledPackageSource(logger, item.Name, item.SourceUri, item.ProtocolVersion);
            }

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
                if (logger is not null)
                {
                    LogEnabledPackageSource(
                        logger,
                        itemClone.Name,
                        itemClone.SourceUri,
                        itemClone.ProtocolVersion
                    );
                }
                yield return itemClone;
            }
        }
    }

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
}
