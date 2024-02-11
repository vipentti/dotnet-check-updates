// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.ProjectModel;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core;

internal record PackageVersionUpgrade(string Id, VersionRange Version)
{
    public PackageReference ToPackageReference() => new(Id, Version);

    public string GetVersionString() => ToPackageReference().GetVersionString();

    public static PackageVersionUpgrade From(string name, string version) =>
        new(name, VersionRange.Parse(version));
}
