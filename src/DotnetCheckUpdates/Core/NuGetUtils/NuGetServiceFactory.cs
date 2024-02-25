// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class NuGetServiceFactory(IServiceProvider serviceProvider)
{
    public INuGetService CreateService(PackageSource source)
    {
        var cache = serviceProvider.GetRequiredService<SourceCacheContext>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var repository = Repository.Factory.GetCoreV3(source);

        if (source.ProtocolVersion == 3)
        {
            return new DefaultNuGetService(
                cache,
                repository,
                loggerFactory.CreateLogger<DefaultNuGetService>(),
                serviceProvider.GetRequiredService<NuGetApiClient>()
            );
        }
        else
        {
            return new StandardNuGetService(loggerFactory, cache, repository);
        }
    }
}
