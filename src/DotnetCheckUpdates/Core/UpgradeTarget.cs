// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DotnetCheckUpdates.Core;

public enum UpgradeTarget
{
    /// <summary>
    /// Latest non-pre-release version
    /// </summary>
    Latest,

    /// <summary>
    /// Latest version, including pre-releases
    /// </summary>
    Greatest,

    /// <summary>
    /// Latest major version
    /// </summary>
    Major,

    /// <summary>
    /// Latest minor version
    /// </summary>
    Minor,

    /// <summary>
    /// Latest patch version
    /// </summary>
    Patch,

    /// <summary>
    /// Latest major version including pre-releases
    /// </summary>
    PrereleaseMajor,

    /// <summary>
    /// Latest minor version including pre-releases
    /// </summary>
    PrereleaseMinor,

    /// <summary>
    /// Latest patch version including pre-releases
    /// </summary>
    PrereleasePatch,
}

internal static class UpgradeTargetExtensions
{
    public static bool IncludePreReleases(this UpgradeTarget target) =>
        target switch
        {
            UpgradeTarget.Greatest => true,
            UpgradeTarget.PrereleaseMajor => true,
            UpgradeTarget.PrereleaseMinor => true,
            UpgradeTarget.PrereleasePatch => true,
            _ => false,
        };

    public static string DisplayName(this UpgradeTarget target) =>
        target switch
        {
            UpgradeTarget.Latest => "latest",
            UpgradeTarget.Greatest => "greatest",
            UpgradeTarget.Major => "major",
            UpgradeTarget.Minor => "minor",
            UpgradeTarget.Patch => "patch",
            _ => target.ToString().ToLowerInvariant(),
        };
}

internal class UpgradeTargetConverter : TypeConverter
{
    private static readonly Dictionary<string, UpgradeTarget> s_additionalNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pre-major"] = UpgradeTarget.PrereleaseMajor,
            ["pre-minor"] = UpgradeTarget.PrereleaseMinor,
            ["pre-patch"] = UpgradeTarget.PrereleasePatch,
        };

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection? GetStandardValues(ITypeDescriptorContext? context) =>
        new(GetValidValues().ToList());

    private static readonly string s_validValues = string.Join("', '", GetValidValues());

    private static IEnumerable<string> GetValidValues()
    {
        foreach (var value in Enum.GetNames<UpgradeTarget>())
        {
            yield return value;
        }

        foreach (var key in s_additionalNames.Keys)
        {
            yield return key;
        }
    }

    public override bool CanConvertTo(
        ITypeDescriptorContext? context,
        [NotNullWhen(true)] Type? destinationType
    )
    {
        if (destinationType == typeof(string))
        {
            return true;
        }

        return base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType
    )
    {
        if (value is UpgradeTarget target && destinationType == typeof(string))
        {
            return target.ToString().ToLowerInvariant();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value
    )
    {
        if (value is string stringValue)
        {
            if (s_additionalNames.TryGetValue(stringValue, out var res))
            {
                return res;
            }

            if (Enum.TryParse<UpgradeTarget>(stringValue, ignoreCase: true, out var upgradeTarget))
            {
                return upgradeTarget;
            }
        }

        throw new FormatException(
            $"Failed to convert '{value}' to UpgradeTarget. Valid values are '{s_validValues}'"
        );
    }
}
