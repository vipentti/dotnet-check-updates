// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class NuGetPackageSourceFeature
{
    public INuGetPackageSourceProvider? PackageSourceProvider { get; set; }
}
