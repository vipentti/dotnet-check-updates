// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal partial class MultiSourceNuGetService(
    ILogger<MultiSourceNuGetService> logger,
    NuGetPackageSourceProvider packageSourceProvider,
    NuGetServiceFactory serviceFactory
) : INuGetService
{
    private readonly ImmutableArray<INuGetService> _nuGetServices = packageSourceProvider
        .GetPackageSources()
        .Select(serviceFactory.CreateService)
        .ToImmutableArray();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting {MethodName}")]
    private static partial void LogStarting(ILogger logger, string methodName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Data resolved by {TypeName}")]
    private static partial void LogPackagesResolvedByService(ILogger logger, string typeName);

    [LoggerMessage(Level = LogLevel.Error, Message = "{TypeName} Failed to get data")]
    private static partial void LogFailed(ILogger logger, Exception exception, string typeName);

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken = default
    )
    {
        LogStarting(logger, nameof(GetPackageVersionsAsync));

        foreach (var service in _nuGetServices)
        {
            try
            {
                var found = await service.GetPackageVersionsAsync(packageId, cancellationToken);

                if (found is not null && found.Any())
                {
                    LogPackagesResolvedByService(logger, service.GetType().Name);
                    return found;
                }
            }
            // Ensure cancellations are propagated
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogFailed(logger, ex, service.GetType().Name);
            }
        }

        return Enumerable.Empty<NuGetVersion>();
    }

    public async Task<ImmutableHashSet<NuGetFramework>> GetSupportedFrameworksAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken = default
    )
    {
        LogStarting(logger, nameof(GetSupportedFrameworksAsync));

        foreach (var service in _nuGetServices)
        {
            try
            {
                var found = await service.GetSupportedFrameworksAsync(
                    packageId,
                    version,
                    cancellationToken
                );

                if (found is not null && !found.IsEmpty)
                {
                    LogPackagesResolvedByService(logger, service.GetType().Name);
                    return found;
                }
            }
            // Ensure cancellations are propagated
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogFailed(logger, ex, service.GetType().Name);
            }
        }

        return ImmutableHashSet<NuGetFramework>.Empty;
    }
}
