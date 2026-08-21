// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal sealed record JsonProjectCheckResult(
    string CanonicalPath,
    string Kind,
    int PackageCount,
    ImmutableArray<NuGetFramework> EffectiveFrameworks,
    ImmutableArray<(
        PackageReference Original,
        VersionRange? TargetVersion,
        UpgradeType UpgradeType,
        ImmutableArray<NuGetFramework> ApplicableFrameworks
    )> PackageResults
);

internal static class JsonResultBuilder
{
    public static JsonOutputDocument Build(
        ImmutableArray<JsonProjectCheckResult> results,
        ImmutableDictionary<string, string[]> solutionsCanonical,
        string canonicalCwd,
        bool showAbsolute,
        bool upgradeRequested,
        bool upgradesApplied
    )
    {
        var solutions = solutionsCanonical
            .Select(kvp =>
            {
                var projects = kvp
                    .Value.Distinct(StringComparer.Ordinal)
                    .Select(it => JsonPathHelper.ToJsonDisplayPath(it, canonicalCwd, showAbsolute))
                    .OrderBy(it => it, StringComparer.Ordinal)
                    .ToList();

                return new JsonSolution
                {
                    Path = JsonPathHelper.ToJsonDisplayPath(kvp.Key, canonicalCwd, showAbsolute),
                    Projects = projects,
                };
            })
            .OrderBy(it => it.Path, StringComparer.Ordinal)
            .ToList();

        var checkedFiles = results
            .Select(r =>
                (
                    Result: r,
                    JsonPath: JsonPathHelper.ToJsonDisplayPath(
                        r.CanonicalPath,
                        canonicalCwd,
                        showAbsolute
                    )
                )
            )
            .OrderBy(it => it.JsonPath, StringComparer.Ordinal)
            .Select(it => it.Result)
            .Select(r =>
            {
                var targetFrameworks = r
                    .EffectiveFrameworks.Select(it => it.GetShortFolderName())
                    .OrderBy(it => it, StringComparer.Ordinal)
                    .ToList();

                var packages = new List<JsonPackage>();
                foreach (var pr in r.PackageResults)
                {
                    var applicableFrameworks = pr
                        .ApplicableFrameworks.Select(it => it.GetShortFolderName())
                        .OrderBy(it => it, StringComparer.Ordinal)
                        .ToList();

                    string? currentVersion = pr.Original.HasVersion
                        ? pr.Original.GetVersionString()
                        : null;

                    string? targetVersion = pr.TargetVersion is not null
                        ? pr.TargetVersion.VersionString(pr.Original.Version.OriginalString)
                        : null;

                    string? upgradeType = pr.TargetVersion is null
                        ? null
                        : JsonUpgradeTypeTokens.FromUpgradeType(pr.UpgradeType);

                    packages.Add(
                        new JsonPackage
                        {
                            Name = pr.Original.Name,
                            CurrentVersion = currentVersion,
                            TargetVersion = targetVersion,
                            UpgradeType = upgradeType,
                            ApplicableFrameworks = applicableFrameworks,
                        }
                    );
                }

                var sortedPackages = packages
                    .Select((pkg, idx) => (pkg, idx))
                    .OrderBy(it => it.pkg.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(it => it.pkg.Name, StringComparer.Ordinal)
                    .ThenBy(it => it.pkg.CurrentVersion, StringComparer.Ordinal)
                    .ThenBy(it => it.pkg.TargetVersion, StringComparer.Ordinal)
                    .ThenBy(it => it.pkg.UpgradeType, StringComparer.Ordinal)
                    .ThenBy(
                        it => string.Join("\0", it.pkg.ApplicableFrameworks),
                        StringComparer.Ordinal
                    )
                    .ThenBy(it => it.idx)
                    .Select(it => it.pkg)
                    .ToList();

                return new JsonCheckedFile
                {
                    Path = JsonPathHelper.ToJsonDisplayPath(
                        r.CanonicalPath,
                        canonicalCwd,
                        showAbsolute
                    ),
                    Kind = r.Kind,
                    PackageCount = r.PackageCount,
                    TargetFrameworks = targetFrameworks,
                    Packages = sortedPackages,
                };
            })
            .ToList();

        return new JsonOutputDocument
        {
            SchemaVersion = 1,
            UpgradeRequested = upgradeRequested,
            UpgradesApplied = upgradesApplied,
            Solutions = solutions,
            CheckedFiles = checkedFiles,
        };
    }
}
