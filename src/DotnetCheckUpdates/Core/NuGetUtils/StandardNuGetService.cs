// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class StandardNuGetService(
    ILoggerFactory logger,
    SourceCacheContext sourceCacheContext,
    SourceRepository repository,
    NuGetSettingsProvider nuGetSettings
) : INuGetService
{
    private readonly SourceRepository _repository = repository;
    private readonly SourceCacheContext _sourceCacheContext = sourceCacheContext;
    private readonly NuGetLoggerAdapter _loggerAdapter = new(logger.CreateLogger("NuGetLogger"));
    private readonly ILogger _logger = logger.CreateLogger(nameof(StandardNuGetService));

    public async Task<ImmutableHashSet<NuGetFramework>> GetSupportedFrameworksAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken = default
    )
    {
        var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>(
            cancellationToken
        );

        if (metadataResource is null)
        {
            return s_noFrameworks;
        }

        var id = new PackageIdentity(packageId, new(version));

        var its = await metadataResource.GetMetadataAsync(
            id,
            _sourceCacheContext,
            _loggerAdapter,
            cancellationToken
        );

        if (its is null)
        {
            return s_noFrameworks;
        }

        var items = its.DependencySets.Select(it => it.TargetFramework).ToImmutableHashSet();

        if (items.IsEmpty)
        {
            var downloadResource = await _repository.GetResourceAsync<DownloadResource>(
                cancellationToken
            );

            if (downloadResource is null)
            {
                return s_noFrameworks;
            }

            var result = await downloadResource.GetDownloadResourceResultAsync(
                id,
                new PackageDownloadContext(_sourceCacheContext),
                SettingsUtility.GetGlobalPackagesFolder(nuGetSettings.NuGetSettings),
                _loggerAdapter,
                cancellationToken
            );

            if (result?.PackageReader is null)
            {
                return s_noFrameworks;
            }

            return (
                await result.PackageReader.GetSupportedFrameworksAsync(cancellationToken)
            ).ToImmutableHashSet();
        }

        return items;
    }

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken = default
    )
    {
        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>(
            cancellationToken
        );

        if (resource is null)
        {
            return Enumerable.Empty<NuGetVersion>();
        }

        return await resource.GetAllVersionsAsync(
            packageId,
            _sourceCacheContext,
            _loggerAdapter,
            cancellationToken
        );
    }

    private static readonly ImmutableHashSet<NuGetFramework> s_noFrameworks =
        ImmutableHashSet<NuGetFramework>.Empty;
}
