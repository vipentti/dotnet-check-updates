// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class DefaultNuGetService : INuGetService
{
    private readonly NuGetApiClient _nuGetApiClient;
    private readonly SourceCacheContext _sourceCacheContext;
    private readonly SourceRepository _sourceRepository;
    private readonly NuGetLoggerAdapter _loggerAdapter;

    public DefaultNuGetService(
        SourceCacheContext sourceCacheContext,
        SourceRepository sourceRepository,
        ILogger<DefaultNuGetService> logger,
        NuGetApiClient nuGetApiClient
    )
    {
        _sourceCacheContext = sourceCacheContext;
        _sourceRepository = sourceRepository;
        _loggerAdapter = new NuGetLoggerAdapter(logger);
        _nuGetApiClient = nuGetApiClient;
    }

    private FindPackageByIdResource? _findPackageByIdResource;

    private async ValueTask<FindPackageByIdResource> GetPackageByIdResourceAsync(
        CancellationToken cancellationToken
    )
    {
        _findPackageByIdResource ??=
            await _sourceRepository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        return _findPackageByIdResource;
    }

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken = default
    )
    {
        var resource = await GetPackageByIdResourceAsync(cancellationToken);

        return await resource.GetAllVersionsAsync(
            packageId,
            _sourceCacheContext,
            _loggerAdapter,
            cancellationToken
        );
    }

    public async Task<ImmutableHashSet<NuGetFramework>> GetSupportedFrameworksAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken = default
    )
    {
        var fws = await _nuGetApiClient.GetSupportedFrameworksAsync(
            packageId,
            version,
            cancellationToken
        );

        if (fws.Count == 0)
        {
            fws = await _nuGetApiClient.GetSupportedFrameworksFromCatalog(
                packageId,
                version,
                cancellationToken
            );
        }

        return fws;
    }
}
