// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Net.Http.Json;
using System.Text.Json;
using DotnetCheckUpdates.Core.Utils;
using Flurl;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class NuGetApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions s_jsonSerializerOptions =
        new() { AllowTrailingCommas = true, PropertyNameCaseInsensitive = true };

    public NuGetApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ImmutableHashSet<NuGetFramework>> GetSupportedFrameworksAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken = default
    )
    {
        packageId = packageId.ToLowerInvariant();
        var url = "v3-flatcontainer".AppendPathSegments(
            packageId,
            version.ToLowerInvariant(),
            packageId + ".nuspec"
        );
        try
        {
            using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);

            var reader = new NuspecReader(stream);

            var asm = reader.GetFrameworkAssemblyGroups();
            var deps = reader.GetDependencyGroups();
            var refs = reader.GetReferenceGroups();

            var frameworks = ImmutableHashSet.CreateBuilder<NuGetFramework>();
            frameworks.AddRange(asm.Select(it => it.TargetFramework));
            frameworks.AddRange(deps.Select(it => it.TargetFramework));
            frameworks.AddRange(refs.Select(it => it.TargetFramework));

            return frameworks.ToImmutable();
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to get {url}", ex);
        }
    }

    public async Task<ImmutableHashSet<NuGetFramework>> GetSupportedFrameworksFromCatalog(
        string packageId,
        string version,
        CancellationToken cancellationToken = default
    )
    {
        var url = "v3".AppendPathSegments(
            "registration5-gz-semver2",
            packageId.ToLowerInvariant(),
            version.ToLowerInvariant() + ".json"
        );

        try
        {
            using var doc = await _httpClient.GetFromJsonAsync<JsonDocument>(
                url,
                s_jsonSerializerOptions,
                cancellationToken
            );

            if (
                doc?.RootElement.TryGetNonNullStringProperty("catalogEntry", out var catalogEntry)
                    is true
                && Url.IsValid(catalogEntry)
            )
            {
                using var catalogResponse = await _httpClient.GetFromJsonAsync<JsonDocument>(
                    catalogEntry,
                    cancellationToken
                );

                return catalogResponse.ReadFrameworksFromCatalog();
            }

            return ImmutableHashSet<NuGetFramework>.Empty;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Failed to get {url}", ex);
        }
    }
}
