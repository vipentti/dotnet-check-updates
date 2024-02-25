// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class StandardNuGetService(
    ILoggerFactory logger,
    SourceCacheContext sourceCacheContext,
    SourceRepository repository
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
            return ImmutableHashSet<NuGetFramework>.Empty;
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
            return ImmutableHashSet<NuGetFramework>.Empty;
        }

        return its.DependencySets.Select(it => it.TargetFramework).ToImmutableHashSet();
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
}
