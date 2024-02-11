// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace DotnetCheckUpdates;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection builder;

    public TypeRegistrar(IServiceCollection builder)
    {
        this.builder = builder;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(this.builder.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        this.builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        this.builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        this.builder.AddSingleton(service, _ => factory());
    }
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider provider;

    public TypeResolver(IServiceProvider provider) =>
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public object? Resolve(Type? type) => type == null ? null : this.provider.GetService(type);

    public void Dispose()
    {
        if (this.provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
