// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotnetCheckUpdates.Core;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSingletonVia<TInterface, TImplementation>(
        this IServiceCollection services
    )
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.TryAddSingleton<TImplementation>();
        services.TryAddSingleton<TInterface>(svc => svc.GetRequiredService<TImplementation>());
        return services;
    }
}
