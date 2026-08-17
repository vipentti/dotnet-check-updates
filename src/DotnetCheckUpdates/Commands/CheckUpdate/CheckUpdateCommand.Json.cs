// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = null,
    };

    private async Task ExecuteJsonAsync(
        string canonicalCwd,
        string? canonicalProject,
        string? canonicalSolution,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        var (solutionProjectMap, projectFiles) = await DiscoverForJson(
            canonicalCwd,
            canonicalProject,
            canonicalSolution,
            settings
        );

        var canonicalGroups = BuildCanonicalGroups(projectFiles, _fileSystem);

        var representativeByCanonical = canonicalGroups.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value[0],
            StringComparer.Ordinal
        );

        var uniqueCanonicalPaths = canonicalGroups.Keys
            .OrderBy(it => it, StringComparer.Ordinal)
            .ToArray();

        var solutionsCanonical = BuildSolutionsCanonical(
            solutionProjectMap,
            representativeByCanonical
        );

        var includeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Include);
        var excludeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Exclude);

        var originalByCanonical = new Dictionary<string, ProjectFile>(StringComparer.Ordinal);
        var effectiveFrameworksByCanonical = new Dictionary<
            string,
            ImmutableArray<NuGetFramework>
        >(StringComparer.Ordinal);
        var packageCountsByCanonical = new Dictionary<string, int>(StringComparer.Ordinal);

        var projects = new List<ProjectFile>(uniqueCanonicalPaths.Length);

        foreach (var canonical in uniqueCanonicalPaths)
        {
            var project = await _projectReader.ReadProjectFile(canonical);
            projects.Add(project);
            originalByCanonical[canonical] = project;
        }

        var allSpecifiedTargetFrameworks = projects
            .SelectMany(it => it.TargetFrameworks)
            .Distinct()
            .ToImmutableArray();

        for (var i = 0; i < projects.Count; i++)
        {
            var proj = projects[i];
            var canonical = uniqueCanonicalPaths[i];

            var filtered = ApplyFilters(proj, includeFilters, excludeFilters);
            var effective = GetEffectiveFrameworks(filtered, allSpecifiedTargetFrameworks);

            if (filtered.TargetFrameworks.Length == 0 && effective.Length > 0)
            {
                LogFrameworkUpdated(_logger, filtered.FilePath, filtered.PackageCount, effective);
                filtered = filtered with { TargetFrameworks = effective };
            }

            projects[i] = filtered;
            originalByCanonical[canonical] = filtered;
            effectiveFrameworksByCanonical[canonical] = filtered.TargetFrameworks;
            packageCountsByCanonical[canonical] = filtered.PackageCount;
        }

        var upgradedByCanonical = await GetUpgradedByCanonical(
            settings,
            uniqueCanonicalPaths,
            originalByCanonical,
            cancellationToken
        );

        var appliedByCanonical = new Dictionary<string, bool>(StringComparer.Ordinal);

        bool upgradesApplied;
        if (settings.Upgrade)
        {
            upgradesApplied = await ApplyUpgradesForJson(
                upgradedByCanonical,
                appliedByCanonical
            );

            if (settings.Restore && upgradesApplied)
            {
                await RestoreForJson(solutionsCanonical, upgradedByCanonical, appliedByCanonical);
            }
        }
        else
        {
            upgradesApplied = false;
            foreach (var kvp in upgradedByCanonical)
            {
                appliedByCanonical[kvp.Key] = false;
            }
        }

        var results = BuildJsonResults(
            uniqueCanonicalPaths,
            originalByCanonical,
            effectiveFrameworksByCanonical,
            packageCountsByCanonical,
            upgradedByCanonical,
            settings
        );

        var document = JsonResultBuilder.Build(
            results,
            solutionsCanonical,
            canonicalCwd,
            settings.ShowAbsolute,
            settings.Upgrade,
            upgradesApplied
        );

        await WriteJsonDocument(document);
    }

    private async Task<(
        ImmutableDictionary<string, string[]> SolutionMap,
        ImmutableArray<string> ProjectFiles
    )> DiscoverForJson(
        string canonicalCwd,
        string? canonicalProject,
        string? canonicalSolution,
        Settings settings
    )
    {
        var (projectFiles, solutionProjectMap) =
            await _projectDiscovery.DiscoverProjectsAndSolutions(
                new()
                {
                    Cwd = canonicalCwd,
                    Recurse = settings.Recurse,
                    Depth = settings.Depth,
                    Project = canonicalProject,
                    Solution = canonicalSolution,
                }
            );

        var canonicalProjectFiles = projectFiles
            .Select(it => _fileSystem.Path.GetFullPath(it))
            .ToImmutableArray();

        var canonicalSolutionMap = solutionProjectMap.ToImmutableDictionary(
            kvp => _fileSystem.Path.GetFullPath(kvp.Key),
            kvp => kvp.Value.Select(it => _fileSystem.Path.GetFullPath(it)).ToArray(),
            StringComparer.Ordinal
        );

        return (canonicalSolutionMap, canonicalProjectFiles);
    }

    private static Dictionary<string, List<string>> BuildCanonicalGroups(
        ImmutableArray<string> projectFiles,
        System.IO.Abstractions.IFileSystem fileSystem
    )
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in projectFiles)
        {
            var canonical = fileSystem.Path.GetFullPath(file);
            if (!groups.TryGetValue(canonical, out var list))
            {
                list = [];
                groups[canonical] = list;
            }
            list.Add(canonical);
        }
        return groups;
    }

    private static ImmutableDictionary<string, string[]> BuildSolutionsCanonical(
        ImmutableDictionary<string, string[]> solutionProjectMap,
        Dictionary<string, string> representativeByCanonical
    )
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string[]>(
            StringComparer.Ordinal
        );

        foreach (var kvp in solutionProjectMap)
        {
            var canonicalSolution = kvp.Key;
            var mapped = kvp.Value.Select(it =>
                    representativeByCanonical.TryGetValue(it, out var rep) ? rep : it
                )
                .Distinct(StringComparer.Ordinal)
                .OrderBy(it => it, StringComparer.Ordinal)
                .ToArray();
            builder[canonicalSolution] = mapped;
        }

        return builder.ToImmutable();
    }

    private static ProjectFile ApplyFilters(
        ProjectFile project,
        ImmutableArray<Core.Filter> includeFilters,
        ImmutableArray<Core.Filter> excludeFilters
    )
    {
        var packages = ImmutableArray.CreateBuilder<PackageReference>(project.PackageCount);
        packages.AddRange(project.PackageReferences);

        if (includeFilters.Length > 0)
        {
            for (var i = packages.Count - 1; i >= 0; --i)
            {
                var pkgName = packages[i].Name;
                if (!includeFilters.Any(it => it.IsMatch(pkgName)))
                {
                    packages.RemoveAt(i);
                }
            }
        }

        if (excludeFilters.Length > 0)
        {
            for (var i = packages.Count - 1; i >= 0; --i)
            {
                var pkgName = packages[i].Name;
                if (excludeFilters.Any(it => it.IsMatch(pkgName)))
                {
                    packages.RemoveAt(i);
                }
            }
        }

        return project with { PackageReferences = packages.ToImmutable() };
    }

    private static ImmutableArray<NuGetFramework> GetEffectiveFrameworks(
        ProjectFile project,
        ImmutableArray<NuGetFramework> allFrameworks
    )
    {
        if (project.TargetFrameworks.Length == 0)
        {
            return allFrameworks;
        }

        return project.TargetFrameworks;
    }

    private async Task<Dictionary<string, (ProjectFile Original, ProjectFile Upgraded)>> GetUpgradedByCanonical(
        Settings settings,
        string[] uniqueCanonicalPaths,
        Dictionary<string, ProjectFile> originalByCanonical,
        CancellationToken cancellationToken
    )
    {
        var upgradedByCanonical = new Dictionary<
            string,
            (ProjectFile Original, ProjectFile Upgraded)
        >(StringComparer.Ordinal);

        InitializePackageService();

        foreach (var canonical in uniqueCanonicalPaths)
        {
            var project = originalByCanonical[canonical];

            var (_, packages) = await CheckUpdateCommandHelpers.GetProjectPackageVersions(
                project,
                _packageService,
                progress: null,
                settings.Concurrency,
                settings.Target,
                _logger,
                cancellationToken
            );

            var upgraded = project.UpdatePackageReferences(packages);
            upgradedByCanonical[canonical] = (project, upgraded);
        }

        return upgradedByCanonical;
    }

    private async Task<bool> ApplyUpgradesForJson(
        Dictionary<string, (ProjectFile Original, ProjectFile Upgraded)> upgradedByCanonical,
        Dictionary<string, bool> appliedByCanonical
    )
    {
        var anyApplied = false;

        foreach (var kvp in upgradedByCanonical.OrderBy(it => it.Key, StringComparer.Ordinal))
        {
            var canonical = kvp.Key;
            var (original, upgraded) = kvp.Value;
            var didUpdate = !AreProjectsEqual(original, upgraded);
            if (!didUpdate)
            {
                appliedByCanonical[canonical] = false;
                continue;
            }

            upgraded.Save(_fileSystem);
            appliedByCanonical[canonical] = true;
            anyApplied = true;
        }

        return anyApplied;
    }

    private static bool AreProjectsEqual(ProjectFile original, ProjectFile upgraded)
    {
        if (original.PackageReferences.Length != upgraded.PackageReferences.Length)
        {
            return false;
        }

        for (var i = 0; i < original.PackageReferences.Length; i++)
        {
            if (!original.PackageReferences[i].Version.Equals(upgraded.PackageReferences[i].Version))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<JsonProjectCheckResult> BuildJsonResults(
        string[] uniqueCanonicalPaths,
        Dictionary<string, ProjectFile> originalByCanonical,
        Dictionary<string, ImmutableArray<NuGetFramework>> effectiveFrameworksByCanonical,
        Dictionary<string, int> packageCountsByCanonical,
        Dictionary<string, (ProjectFile Original, ProjectFile Upgraded)> upgradedByCanonical,
        Settings settings
    )
    {
        var builder = ImmutableArray.CreateBuilder<JsonProjectCheckResult>(
            uniqueCanonicalPaths.Length
        );

        foreach (var canonical in uniqueCanonicalPaths)
        {
            var original = originalByCanonical[canonical];
            var upgraded = upgradedByCanonical[canonical].Upgraded;
            var effectiveFrameworks = effectiveFrameworksByCanonical[canonical];
            var packageCount = packageCountsByCanonical[canonical];
            var kind = JsonPathHelper.GetKind(canonical);

            var packageResults = ImmutableArray.CreateBuilder<(
                PackageReference Original,
                VersionRange? TargetVersion,
                UpgradeType UpgradeType,
                ImmutableArray<NuGetFramework> ApplicableFrameworks
            )>(original.PackageReferences.Length);

            for (var i = 0; i < original.PackageReferences.Length; i++)
            {
                var originalRef = original.PackageReferences[i];
                var upgradedRef = upgraded.PackageReferences[i];

                var isUpgrade = !originalRef.Version.Equals(upgradedRef.Version);
                VersionRange? targetVersion = isUpgrade ? upgradedRef.Version : null;

                if (!settings.List && targetVersion is null)
                {
                    continue;
                }

                var applicableFrameworks = originalRef.GetApplicableFrameworks(
                    effectiveFrameworks
                );

                var upgradeType = targetVersion is null
                    ? UpgradeType.None
                    : originalRef.Version.GetUpgradeTypeTo(targetVersion);

                packageResults.Add(
                    (originalRef, targetVersion, upgradeType, applicableFrameworks)
                );
            }

            builder.Add(
                new JsonProjectCheckResult(
                    canonical,
                    kind,
                    packageCount,
                    effectiveFrameworks,
                    original.PackageReferences,
                    packageResults.ToImmutable(),
                    original,
                    upgraded
                )
            );
        }

        return builder.ToImmutable();
    }

    private static async Task RestoreForJson(
        ImmutableDictionary<string, string[]> solutionsCanonical,
        Dictionary<string, (ProjectFile Original, ProjectFile Upgraded)> upgradedByCanonical,
        Dictionary<string, bool> appliedByCanonical
    )
    {
        var hasSolutions = solutionsCanonical.Count > 0;
        var upgradedFiles = upgradedByCanonical
            .Where(kvp => appliedByCanonical.TryGetValue(kvp.Key, out var applied) && applied)
            .Select(kvp => kvp.Value.Upgraded)
            .ToList();

        if (upgradedFiles.Count == 0)
        {
            return;
        }

        var dotnet = Cli.Wrap("dotnet")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Console.Error.WriteLine(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Console.Error.WriteLine(line)));

        List<Command> cmds;

        if (hasSolutions)
        {
            cmds = solutionsCanonical.Keys
                .Select(it => dotnet.WithArguments(["restore", it]))
                .ToList();
        }
        else
        {
            cmds = upgradedFiles
                .Select(it => dotnet.WithArguments(["restore", it.FilePath]))
                .ToList();
        }

        foreach (var restoreCmd in cmds)
        {
#pragma warning disable S6966
            Console.Error.WriteLine(restoreCmd.ToString());
#pragma warning restore S6966
            await restoreCmd.ExecuteAsync();
        }
    }

    private static async Task WriteJsonDocument(JsonOutputDocument document)
    {
        var json = JsonSerializer.Serialize(document, s_jsonOptions);
        await Console.Out.WriteLineAsync(json);
    }
}