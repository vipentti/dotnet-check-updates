// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core;

public enum UpgradeType
{
    None,
    Major,
    Minor,
    Patch,
    Release,
}

internal static class UpgradeTypeExtensions
{
    public static UpgradeType GetUpgradeTypeTo(this VersionRange lhs, VersionRange rhs)
    {
        if ((lhs.IsExact() && rhs.IsExact()) || (lhs.HasLowerBound && rhs.HasLowerBound))
        {
            var lhsmin = lhs.MinVersion!;
            var rhsmin = rhs.MinVersion!;

            if (rhsmin.Major > lhsmin.Major)
            {
                return UpgradeType.Major;
            }

            if (rhsmin.Minor > lhsmin.Minor)
            {
                return UpgradeType.Minor;
            }

            if (rhsmin.Patch > lhsmin.Patch)
            {
                return UpgradeType.Patch;
            }

            // https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#pre-release-versions
            if (StringComparer.OrdinalIgnoreCase.Compare(rhsmin.Release, lhsmin.Release) > 0)
            {
                return UpgradeType.Release;
            }
        }

        return UpgradeType.None;
    }
}
