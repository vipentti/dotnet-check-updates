// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal record PackageReference(string Name, VersionRange Version) : IPackageReference
{
    public int ReferenceId { get; init; } = -1;

    public ImmutableArray<TargetFrameworkCondition> TargetFrameworkConditions { get; init; } =
        ImmutableArray<TargetFrameworkCondition>.Empty;

    internal VersionRange? OriginalRange { get; private set; }

    protected PackageReference(PackageReference original)
    {
        Name = original.Name;
        Version = original.Version;
        ReferenceId = original.ReferenceId;
        TargetFrameworkConditions = original.TargetFrameworkConditions;
        OriginalRange = original.OriginalRange ?? original.Version;
    }

    public string UpgradeKey => ReferenceId >= 0 ? $"{ReferenceId}:{Name}" : Name;

    public bool HasTargetFrameworkConditions => !TargetFrameworkConditions.IsDefaultOrEmpty;

    public string DisplayName =>
        !HasTargetFrameworkConditions
            ? Name
            : $"{Name} ({string.Join(" Or ", GetTargetFrameworkConditionGroups().Select(group => string.Join(" And ", group.Select(it => it.ToDisplayString()))))})";

    public bool HasVersion => !Version.Equals(VersionRange.None);

    public bool HasName(string name) =>
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

    public bool HasReferenceId(int referenceId) => ReferenceId == referenceId;

    public ImmutableArray<NuGetFramework> GetApplicableFrameworks(
        IEnumerable<NuGetFramework> targetFrameworks
    )
    {
        var frameworks = targetFrameworks.ToImmutableArray();

        if (!HasTargetFrameworkConditions)
        {
            return frameworks;
        }

        var builder = ImmutableArray.CreateBuilder<NuGetFramework>(frameworks.Length);
        var groups = GetTargetFrameworkConditionGroups();

        foreach (var framework in frameworks)
        {
            var frameworkName = framework.GetShortFolderName();

            if (groups.Any(group => group.All(it => it.IsMatch(frameworkName))))
            {
                builder.Add(framework);
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<
        ImmutableArray<TargetFrameworkCondition>
    > GetTargetFrameworkConditionGroups() =>
        TargetFrameworkConditions
            .GroupBy(it => it.GroupId)
            .OrderBy(it => it.Key)
            .Select(it => it.ToImmutableArray())
            .ToImmutableArray();

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
