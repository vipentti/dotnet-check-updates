// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal record PackageReference(string Name, VersionRange Version) : IPackageReference
{
    internal VersionRange? OriginalRange { get; private set; }

    protected PackageReference(PackageReference original)
    {
        Name = original.Name;
        Version = original.Version;
        OriginalRange = original.OriginalRange ?? original.Version;
    }

    public bool HasVersion => !Version.Equals(VersionRange.None);

    public bool HasName(string name) =>
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

    public static PackageReference From(string name, string version) =>
        new(name, version.ToVersionRange());

    private string? _versionString;

    public string GetVersionString()
    {
        if (!HasVersion)
        {
            return "";
        }

        if (_versionString is not null)
        {
            return _versionString;
        }

        if (OriginalRange is VersionRange original && original.OriginalString is string str)
        {
            return _versionString = Version?.VersionString(str) ?? "";
        }

        return _versionString = Version?.VersionString(null) ?? "";
    }
}
