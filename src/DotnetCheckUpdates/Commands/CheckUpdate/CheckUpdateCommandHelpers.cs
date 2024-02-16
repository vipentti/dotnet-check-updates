// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.ProjectModel;
using Flurl.Util;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal static class CheckUpdateCommandHelpers
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

    public static string GetUpgradedVersionString(PackageReference lhs, PackageReference rhs)
    {
        var type = lhs.Version.GetUpgradeTypeTo(rhs.Version);

        // version string should be in format x.y.z
        // optionally starting with [ or (
        var versionString = rhs.GetVersionString().EscapeMarkup();

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

    public static async Task<(
        ProjectFile project,
        PackageUpgradeVersionDictionary packages
    )> GetProjectPackageVersions(
        ProjectFile project,
        PackageUpgradeService packageService,
        ProgressTask progress,
        int concurrency,
        UpgradeTarget target,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var pkgs = new PackageUpgradeVersionDictionary(project.PackageCount);

        if (logger?.IsEnabled(LogLevel.Trace) is true)
        {
            logger.LogTrace("Upgrading packages for {Project}({PackageCount})", project.FilePath, project.PackageCount);
        }

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

            progress.Increment(1);

            return upgrade;
        }
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
        int longestVersionLength
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

            var node = root.AddNode(projPath);
            var (didUpdatePackages, renderable) = SetupGrid(
                oldproj,
                newproj,
                settings.List,
                longestPackageNameLength,
                longestVersionLength
            );

            if (!didUpdatePackages && !settings.List)
            {
                node.AddNode(
                    new Padder(
                        new Text("All packages match their latest versions."),
                        new(0, 0, 0, 1)
                    )
                );
            }
            else if (didUpdatePackages || settings.List)
            {
                if (didUpdatePackages)
                {
                    upgradedProjects.Add(newproj);
                }

                node.AddNode(new Padder(renderable, new(0, 0, 0, 1)));
            }
        }
    }
}
