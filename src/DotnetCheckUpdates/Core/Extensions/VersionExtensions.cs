// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.Extensions;

internal static class VersionExtensions
{
    public static bool IsExact([NotNullWhen(true)] this VersionRange? range) =>
        range?.HasLowerAndUpperBounds == true && range.MinVersion == range.MaxVersion;

    public static T NotNull<T>(this T? value)
        where T : class => value ?? throw new ArgumentNullException(nameof(value));

    public static bool NewVersionSatisfiesTargetAndRange(
        this VersionRange range,
        NuGetVersion version,
        UpgradeTarget target
    )
    {
        var isExact = range.IsExact();

        // Currently not supporting non-exact versions with an upper bound
        if (!isExact && range.HasUpperBound)
        {
            return false;
        }

        if (isExact)
        {
            var openUpperRange = new VersionRange(range.MinVersion!, range.Float);
            return openUpperRange.SatisfiesTarget(
                current: null,
                considering: version,
                target: target
            );
        }
        else if (range.HasLowerBound && range.IsMinInclusive)
        {
            return range.SatisfiesTarget(current: null, considering: version, target);
        }
        else
        {
            return false;
        }
    }

    public static bool SatisfiesTarget(
        this VersionRange range,
        NuGetVersion? current,
        NuGetVersion considering,
        UpgradeTarget target
    )
    {
        var minVersion = range.MinVersion ?? NuGetVersion.Parse("0.0.0");
        var extraCondition = true;

        switch (target)
        {
            case UpgradeTarget.Latest:
                break;
            case UpgradeTarget.Greatest:
                current ??= range.MinVersion;
                break;
            case UpgradeTarget.PrereleaseMajor:
                minVersion = new NuGetVersion(minVersion.Version, releaseLabel: "0");
                extraCondition = BetterMajor(considering, minVersion);
                break;
            case UpgradeTarget.Major:
                current ??= range.MinVersion;
                extraCondition = BetterMajor(considering, minVersion);
                break;
            case UpgradeTarget.PrereleaseMinor:
                minVersion = new NuGetVersion(minVersion.Version, releaseLabel: "0");
                extraCondition = BetterMinor(considering, minVersion);
                break;
            case UpgradeTarget.Minor:
                current ??= range.MinVersion;
                extraCondition = BetterMinor(considering, minVersion);
                break;
            case UpgradeTarget.PrereleasePatch:
                minVersion = new NuGetVersion(minVersion.Version, releaseLabel: "0");
                extraCondition = BetterPatch(considering, minVersion);
                break;
            case UpgradeTarget.Patch:
                current ??= range.MinVersion;
                extraCondition = BetterPatch(considering, minVersion);
                break;
            default:
                throw new NotImplementedException(target.ToString());
        }

        var floatBehavior = ToNuGetFloatVersion(target);
        FloatRange? floatRange =
            floatBehavior == NuGetVersionFloatBehavior.None ? null : new(floatBehavior, minVersion);

        if (floatRange is not null)
        {
            range = new VersionRange(range, floatRange);
        }

        return extraCondition && range.IsBetter(current, considering);

        static bool BetterMajor(NuGetVersion lhs, NuGetVersion rhs) => lhs.Major > rhs.Major;

        static bool BetterMinor(NuGetVersion lhs, NuGetVersion rhs) =>
            lhs.Major == rhs.Major && lhs.Minor > rhs.Minor;

        static bool BetterPatch(NuGetVersion lhs, NuGetVersion rhs) =>
            lhs.Major == rhs.Major && lhs.Minor == rhs.Minor && lhs.Patch > rhs.Patch;

        static NuGetVersionFloatBehavior ToNuGetFloatVersion(UpgradeTarget target) =>
            target switch
            {
                UpgradeTarget.Latest => NuGetVersionFloatBehavior.None,
                UpgradeTarget.Greatest => NuGetVersionFloatBehavior.AbsoluteLatest,
                UpgradeTarget.Major => NuGetVersionFloatBehavior.Major,
                UpgradeTarget.Minor => NuGetVersionFloatBehavior.Minor,
                UpgradeTarget.Patch => NuGetVersionFloatBehavior.Patch,
                UpgradeTarget.PrereleaseMajor => NuGetVersionFloatBehavior.PrereleaseMajor,
                UpgradeTarget.PrereleaseMinor => NuGetVersionFloatBehavior.PrereleaseMinor,
                UpgradeTarget.PrereleasePatch => NuGetVersionFloatBehavior.PrereleasePatch,
                _ => NuGetVersionFloatBehavior.None,
            };
    }

    public static VersionRange ToVersionRange(this string version)
    {
        return VersionRange.Parse(version);
    }

    [return: NotNullIfNotNull(nameof(version))]
    public static VersionRange? ToVersionRange(this NuGetVersion? version, VersionRange original)
    {
        if (version is null)
        {
            return null;
        }

        if (original.IsExact())
        {
            return new VersionRange(
                minVersion: version,
                includeMinVersion: true,
                maxVersion: version,
                includeMaxVersion: true
            );
        }

        if (original.HasLowerBound && !original.HasUpperBound)
        {
            return new VersionRange(
                minVersion: version,
                includeMinVersion: original.IsMinInclusive,
                maxVersion: null,
                includeMaxVersion: false
            );
        }

        throw new NotImplementedException(original.ToString());
    }

    public static NuGetVersion ToNuGetVersion(this string version)
    {
        return NuGetVersion.Parse(version);
    }

    public static NuGetFramework ToNuGetFramework(this string fw) => NuGetFramework.Parse(fw);

    public static string VersionString(this VersionRange range, string? other = default)
    {
        if (range.IsExact())
        {
            return range.ToShortString();
        }

        if (other is not null && (other.Contains('[') || other.Contains('(')))
        {
            return range.ToNormalizedString().Replace(" ", "");
        }

        if (range.OriginalString is string str && (str.Contains('[') || str.Contains('(')))
        {
            return range.ToNormalizedString().Replace(" ", "");
        }

        return range.ToShortString();
    }
}
