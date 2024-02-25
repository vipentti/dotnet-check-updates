// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Flurl.Util;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal record PackageUpgrade(string Name, VersionRange From, VersionRange To);

internal static partial class CheckUpdateCommandHelpers
{
    public static ImmutableArray<Filter> SplitFilters(string[] strings)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>();

        foreach (var str in strings)
        {
            foreach (
                var part in str.Split(
                    (char[])[' ', ','],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            {
                builder.Add(part);
            }
        }

        return builder
            .OrderBy(it => it, StringComparer.OrdinalIgnoreCase)
            .Select(it => new Filter(it))
            .ToImmutableArray();
    }

    public static string GetUpgradedVersionString(PackageReference lhs, PackageReference rhs) =>
        GetUpgradedVersionString(lhs.Version, rhs.Version, rhs.OriginalRange);

    public static string GetUpgradedVersionString(
        VersionRange from,
        VersionRange to,
        VersionRange? original = default
    )
    {
        var type = from.GetUpgradeTypeTo(to);

        // version string should be in format x.y.z
        // optionally starting with [ or (
        var versionString = to.VersionString(original?.OriginalString).EscapeMarkup();

        if (type == UpgradeType.Major)
        {
            return $"[red]{versionString}[/]";
        }

        if (type == UpgradeType.Minor)
        {
            var parts = versionString.Split('.');
            var major = parts[0];

            var rest = $"[cyan]{string.Join(".", parts[1..])}[/]";

            return $"{major}.{rest}";
        }

        if (type == UpgradeType.Patch)
        {
            var parts = versionString.Split('.');
            var major = parts[0];
            var minor = parts[1];
            var rest = $"[green]{string.Join(".", parts[2..])}[/]";
            return $"{major}.{minor}.{rest}";
        }

        if (type == UpgradeType.Release)
        {
            var parts = versionString.SplitOnFirstOccurence("-");
            var rest = $"[fuchsia]{parts[1]}[/]";
            return $"{parts[0]}-{rest}";
        }

        return versionString;
    }

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Upgrading packages for {Project}({PackageCount})"
    )]
    private static partial void LogUpgradingPackages(
        ILogger logger,
        string project,
        int packageCount
    );

    public static async Task<(
        ProjectFile project,
        PackageUpgradeVersionDictionary packages
    )> GetProjectPackageVersions(
        ProjectFile project,
        PackageUpgradeService packageService,
        ProgressTask? progress,
        int concurrency,
        UpgradeTarget target,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var pkgs = new PackageUpgradeVersionDictionary(project.PackageCount);

        LogUpgradingPackages(logger, project.FilePath, project.PackageCount);

        if (concurrency > 1)
        {
            foreach (var chunk in project.PackageReferences.Chunk(concurrency))
            {
                var results = await Task.WhenAll(chunk.Select(BoundGetBestPackageVersion));

                foreach (var upgrade in results)
                {
                    if (upgrade is not null)
                    {
                        pkgs[upgrade.Id] = upgrade.Version;
                    }
                }
            }
        }
        else
        {
            foreach (var pkgRef in project.PackageReferences)
            {
                var upgrade = await BoundGetBestPackageVersion(pkgRef);

                if (upgrade is not null)
                {
                    pkgs[upgrade.Id] = upgrade.Version;
                }
            }
        }

        return (project, pkgs);

        async Task<PackageVersionUpgrade?> BoundGetBestPackageVersion(PackageReference pkgRef)
        {
            var upgrade = await packageService.GetPackageUpgrade(
                project.TargetFrameworks,
                pkgRef,
                target,
                cancellationToken
            );

            progress?.Increment(1);

            return upgrade;
        }
    }

    public static ImmutableArray<PackageUpgrade> GetProjectPackageUpgrades(
        ProjectFile project,
        PackageUpgradeVersionDictionary upgrades
    )
    {
        ImmutableArray<PackageUpgrade>.Builder? builder = null;

        foreach (var pkgRef in project.PackageReferences)
        {
            if (
                upgrades.TryGetValue(pkgRef.Name, out var version)
                && !pkgRef.Version.Equals(version)
            )
            {
                builder ??= ImmutableArray.CreateBuilder<PackageUpgrade>();
                builder.Add(new(pkgRef.Name, pkgRef.Version, version));
            }
        }

        return builder?.ToImmutable() ?? ImmutableArray<PackageUpgrade>.Empty;
    }

    public static (bool DidUpdate, ImmutableArray<PackageUpgrade> Upgrades) CheckForUpgrades(
        ProjectFile original,
        ProjectFile updated
    )
    {
        var didUpdate = false;
        var upgrades = ImmutableArray.CreateBuilder<PackageUpgrade>();

        foreach (
            var originalPackage in original.PackageReferences.OrderBy(
                it => it.Name,
                StringComparer.Ordinal
            )
        )
        {
            if (
                updated.FindByNameWithIndex(originalPackage.Name)
                    is
                    (int index, PackageReference updatedPackage)
                && !originalPackage.Version.Equals(updatedPackage.Version)
            )
            {
                didUpdate = true;
                upgrades.Add(
                    new(originalPackage.Name, originalPackage.Version, updatedPackage.Version)
                );
            }
        }

        return (didUpdate, upgrades.ToImmutable());
    }

    public static (bool DidUpdate, Renderable Grid) SetupGrid(
        ProjectFile original,
        ProjectFile updated,
        bool list,
        int longestPackageName,
        int longestVersion
    )
    {
        var result = new Grid();

        result.AddColumn();
        result.AddColumn();
        result.AddColumn();
        result.AddColumn();

        var didUpdate = false;

        var versionSpaces = new string(' ', longestVersion);

        foreach (var originalPackage in original.PackageReferences.OrderBy(it => it.Name))
        {
            var nameLengthToPad = longestPackageName - originalPackage.Name.Length;

            var paddedPackageName = new Padder(
                new Text(originalPackage.Name),
                new(0, 0, nameLengthToPad, 0)
            );

            var originalVersionString = originalPackage.GetVersionString();
            var versionLengthToPad = longestVersion - originalVersionString.Length;

            var originalVersion = new Markup(originalVersionString.EscapeMarkup());

            if (
                updated.FindByNameWithIndex(originalPackage.Name)
                    is
                    (int index, PackageReference updatedPackage)
                && !originalPackage.Version.Equals(updatedPackage.Version)
            )
            {
                didUpdate = true;
                result.AddRow(
                    paddedPackageName,
                    new Padder(originalVersion, new Padding(0, 0, versionLengthToPad, 0)),
                    new Markup("→"),
                    new Markup(GetUpgradedVersionString(originalPackage, updatedPackage))
                );
            }
            else if (list)
            {
                result.AddRow(
                    paddedPackageName,
                    // NOTE: Using Padder here for some reason did not align correctly in all cases
                    // so just using spaces seems to work
                    new Text(versionSpaces),
                    new Markup(" "),
                    originalVersion
                );
            }
        }

        return (didUpdate, result);
    }

    public static void SetupGridInTree(
        Func<string, string> formatPath,
        Tree root,
        IEnumerable<ProjectFile> oldProjects,
        IEnumerable<ProjectFile> newProjects,
        List<ProjectFile> upgradedProjects,
        CheckUpdateCommand.Settings settings,
        int longestPackageNameLength,
        int longestVersionLength,
        bool hideIfNoUpgrade = false
    )
    {
        foreach (var (oldproj, newproj) in oldProjects.Zip(newProjects))
        {
            Debug.Assert(oldproj.FilePath == newproj.FilePath);

            if (oldproj.PackageCount == 0)
            {
                continue;
            }

            var projPath = formatPath(oldproj.FilePath);

            var (didUpdatePackages, renderable) = SetupGrid(
                oldproj,
                newproj,
                settings.List,
                longestPackageNameLength,
                longestVersionLength
            );

            if (!didUpdatePackages && !settings.List)
            {
                if (!hideIfNoUpgrade)
                {
                    var node = root.AddNode(projPath);
                    node.AddNode(
                        new Padder(
                            new Text(CommonStrings.AllPackagesMatchLatestVersion),
                            new(0, 0, 0, 1)
                        )
                    );
                }
            }
            else if (didUpdatePackages || settings.List)
            {
                if (didUpdatePackages)
                {
                    upgradedProjects.Add(newproj);
                }

                var node = root.AddNode(projPath);
                node.AddNode(new Padder(renderable, new(0, 0, 0, 1)));
            }
        }
    }
}
