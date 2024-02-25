// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using NuGet.Configuration;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class NuGetPackageSourceProvider(IEnumerable<PackageSource>? sources = default)
    : INuGetPackageSourceProvider
{
    private static readonly IEnumerable<PackageSource> s_defaultSources =
    [
        new PackageSource(NuGetConstants.V3FeedUrl) { ProtocolVersion = 3, }
    ];

    private readonly ImmutableArray<PackageSource> _providedSources =
        sources?.ToImmutableArray() ?? ImmutableArray<PackageSource>.Empty;

    public IEnumerable<PackageSource> GetPackageSources()
    {
        // Default to nuget v3
        if (_providedSources.IsEmpty)
        {
            return s_defaultSources;
        }

        return _providedSources;
    }
}
