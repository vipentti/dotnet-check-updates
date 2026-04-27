// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using NuGet.Configuration;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal interface INuGetPackageSourceProvider
{
    IEnumerable<PackageSource> GetPackageSources();
}
