// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal interface IPackageReference
{
    public string Name { get; }
    public VersionRange Version { get; }

    public bool HasName(string name) =>
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
}
