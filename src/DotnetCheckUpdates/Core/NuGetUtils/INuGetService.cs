// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal interface INuGetService
{
    public Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken = default
    );

    public Task<ImmutableHashSet<NuGetFramework>> GetSupportedFrameworksAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken = default
    );
}
