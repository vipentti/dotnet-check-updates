// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal partial class MultiSourceNuGetService(
    ILogger<MultiSourceNuGetService> logger,
    INuGetPackageSourceProvider packageSourceProvider,
    NuGetServiceFactory serviceFactory
) : INuGetService
{
    private readonly ImmutableArray<INuGetService> _nuGetServices = packageSourceProvider
        .GetPackageSources()
        .Select(serviceFactory.CreateService)
        .ToImmutableArray();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting {MethodName}")]
    private static partial void LogStarting(ILogger logger, string methodName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "{MethodName}: {PackageId} package versions resolved by {TypeName}"
    )]
    private static partial void LogPackagesResolvedByService(
        ILogger logger,
        string methodName,
        string packageId,
        string typeName
    );

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "{MethodName}: {PackageId} {Version} frameworks resolved by {TypeName}"
    )]
    private static partial void LogFrameworksResolvedByService(
        ILogger logger,
        string methodName,
        string packageId,
        string version,
        string typeName
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{MethodName}: {TypeName} Failed to get {PackageId} data"
    )]
    private static partial void LogFailed(
        ILogger logger,
        Exception exception,
        string methodName,
        string typeName,
        string packageId
    );

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken = default
    )
    {
        const string method = nameof(GetPackageVersionsAsync);
        LogStarting(logger, method);

        foreach (var service in _nuGetServices)
        {
            try
            {
                var found = await service.GetPackageVersionsAsync(packageId, cancellationToken);

                if (found?.Any() is true)
                {
                    LogPackagesResolvedByService(logger, method, packageId, service.GetType().Name);
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
                LogFailed(logger, ex, method, service.GetType().Name, packageId);
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
        const string method = nameof(GetSupportedFrameworksAsync);
        LogStarting(logger, method);

        foreach (var service in _nuGetServices)
        {
            try
            {
                var found = await service.GetSupportedFrameworksAsync(
                    packageId,
                    version,
                    cancellationToken
                );

                if (!found.IsEmpty)
                {
                    LogFrameworksResolvedByService(
                        logger,
                        method,
                        packageId,
                        version,
                        service.GetType().Name
                    );
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
                LogFailed(logger, ex, method, service.GetType().Name, packageId);
            }
        }

        return ImmutableHashSet<NuGetFramework>.Empty;
    }
}
