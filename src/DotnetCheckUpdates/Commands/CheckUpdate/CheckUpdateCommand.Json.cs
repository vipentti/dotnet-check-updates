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
        var (solutionProjectMap, projectFiles, propsFiles) = await DiscoverForJson(
            canonicalCwd,
            canonicalProject,
            canonicalSolution,
            settings
        );

        var canonicalSet = projectFiles
            .Select(it => _fileSystem.Path.GetFullPath(it))
            .Distinct(StringComparer.Ordinal)
            .ToImmutableHashSet(StringComparer.Ordinal);

        // Also consider explicit conventional props files (e.g., explicit --project Directory.Build.props) as props provenance
        var uniqueCanonicalPaths = canonicalSet.OrderBy(it => it, StringComparer.Ordinal).ToArray();

        var solutionsCanonical = BuildSolutionsCanonicalFiltered(solutionProjectMap, canonicalSet);

        var includeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Include);
        var excludeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Exclude);

        var originalByCanonical = new Dictionary<string, ProjectFile>(StringComparer.Ordinal);
        var effectiveFrameworksByCanonical = new Dictionary<string, ImmutableArray<NuGetFramework>>(
            StringComparer.Ordinal
        );
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

            var filtered = CheckUpdateCommandHelpers.ApplyFilters(
                proj,
                includeFilters,
                excludeFilters
            );
            var effective = CheckUpdateCommandHelpers.GetEffectiveFrameworks(
                filtered,
                allSpecifiedTargetFrameworks
            );

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

        IReadOnlyList<ProjectFile> savedFiles;
        bool upgradesApplied;
        if (settings.Upgrade)
        {
            savedFiles = await ApplyUpgradesForJson(upgradedByCanonical);
            upgradesApplied = savedFiles.Count > 0;

            if (settings.Restore && upgradesApplied)
            {
                await RestoreForJson(solutionsCanonical, savedFiles);
            }
        }
        else
        {
            upgradesApplied = false;
        }

        var results = BuildJsonResults(
            uniqueCanonicalPaths,
            originalByCanonical,
            effectiveFrameworksByCanonical,
            packageCountsByCanonical,
            upgradedByCanonical,
            propsFiles,
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
        ImmutableArray<string> ProjectFiles,
        ImmutableHashSet<string> PropsFiles
    )> DiscoverForJson(
        string canonicalCwd,
        string? canonicalProject,
        string? canonicalSolution,
        Settings settings
    )
    {
        var result = await _projectDiscovery.DiscoverProjectsAndSolutions(
            new()
            {
                Cwd = canonicalCwd,
                Recurse = settings.Recurse,
                Depth = settings.Depth,
                Project = canonicalProject,
                Solution = canonicalSolution,
            }
        );

        var canonicalProjectFiles = result
            .ProjectFiles.Select(it => _fileSystem.Path.GetFullPath(it))
            .ToImmutableArray();

        var canonicalSolutionMap = result.SolutionProjectMap.ToImmutableDictionary(
            kvp => _fileSystem.Path.GetFullPath(kvp.Key),
            kvp => kvp.Value.Select(it => _fileSystem.Path.GetFullPath(it)).ToArray(),
            StringComparer.Ordinal
        );

        var propsFiles =
            result
                .PropsFiles?.Select(it => _fileSystem.Path.GetFullPath(it))
                .ToImmutableHashSet(StringComparer.Ordinal)
            ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

        return (canonicalSolutionMap, canonicalProjectFiles, propsFiles);
    }

    private static ImmutableDictionary<string, string[]> BuildSolutionsCanonicalFiltered(
        ImmutableDictionary<string, string[]> solutionProjectMap,
        ImmutableHashSet<string> canonicalSet
    )
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string[]>(StringComparer.Ordinal);

        foreach (var kvp in solutionProjectMap)
        {
            var canonicalSolution = kvp.Key;
            var filtered = kvp
                .Value.Where(it =>
                    it.EndsWith(
                        CliConstants.CsProjExtensionWithDot,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || it.EndsWith(
                        CliConstants.FsProjExtensionWithDot,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Where(it => canonicalSet.Contains(it))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(it => it, StringComparer.Ordinal)
                .ToArray();
            builder[canonicalSolution] = filtered;
        }

        return builder.ToImmutable();
    }

    private async Task<
        Dictionary<string, (ProjectFile Original, ProjectFile Upgraded)>
    > GetUpgradedByCanonical(
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

        var serviceInitialized = false;

        foreach (var canonical in uniqueCanonicalPaths)
        {
            var project = originalByCanonical[canonical];

            if (project.PackageReferences.Length == 0)
            {
                upgradedByCanonical[canonical] = (project, project);
                continue;
            }

            if (!serviceInitialized)
            {
                InitializePackageService();
                serviceInitialized = true;
            }

            var (_, packages) = await CheckUpdateCommandHelpers.GetProjectPackageVersions(
                project,
                _packageService!,
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

    private async Task<IReadOnlyList<ProjectFile>> ApplyUpgradesForJson(
        Dictionary<string, (ProjectFile Original, ProjectFile Upgraded)> upgradedByCanonical
    )
    {
        var saved = new List<ProjectFile>();

        foreach (var kvp in upgradedByCanonical.OrderBy(it => it.Key, StringComparer.Ordinal))
        {
            var (original, upgraded) = kvp.Value;
            if (AreProjectsEqual(original, upgraded))
            {
                continue;
            }

            upgraded.Save(_fileSystem);
            saved.Add(upgraded);
        }

        return saved;
    }

    private static bool AreProjectsEqual(ProjectFile original, ProjectFile upgraded)
    {
        if (original.PackageReferences.Length != upgraded.PackageReferences.Length)
        {
            return false;
        }

        for (var i = 0; i < original.PackageReferences.Length; i++)
        {
            if (
                !original.PackageReferences[i].Version.Equals(upgraded.PackageReferences[i].Version)
            )
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
        ImmutableHashSet<string> propsFiles,
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
            string kind;
            if (propsFiles.Contains(canonical))
            {
                var fn = Path.GetFileName(canonical);
                if (fn == CliConstants.DirectoryBuildPropsFileName)
                {
                    kind = JsonOutputKind.DirectoryBuildProps;
                }
                else if (fn == CliConstants.DirectoryPackagesPropsFileName)
                {
                    kind = JsonOutputKind.DirectoryPackagesProps;
                }
                else
                {
                    kind = JsonOutputKind.Project;
                }
            }
            else
            {
                kind = JsonOutputKind.Project;
            }

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

                var applicableFrameworks = originalRef.GetApplicableFrameworks(effectiveFrameworks);

                var upgradeType = targetVersion is null
                    ? UpgradeType.None
                    : originalRef.Version.GetUpgradeTypeTo(targetVersion);

                packageResults.Add((originalRef, targetVersion, upgradeType, applicableFrameworks));
            }

            builder.Add(
                new JsonProjectCheckResult(
                    canonical,
                    kind,
                    packageCount,
                    effectiveFrameworks,
                    packageResults.ToImmutable()
                )
            );
        }

        return builder.ToImmutable();
    }

    private static async Task RestoreForJson(
        ImmutableDictionary<string, string[]> solutionsCanonical,
        IReadOnlyList<ProjectFile> savedFiles
    )
    {
        if (savedFiles.Count == 0)
        {
            return;
        }

        var hasSolutions = solutionsCanonical.Count > 0;

        var dotnet = Cli.Wrap("dotnet")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Console.Error.WriteLine(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Console.Error.WriteLine(line)));

        List<Command> cmds;

        if (hasSolutions)
        {
            cmds = solutionsCanonical
                .Keys.Select(it => dotnet.WithArguments(["restore", it]))
                .ToList();
        }
        else
        {
            cmds = savedFiles.Select(it => dotnet.WithArguments(["restore", it.FilePath])).ToList();
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
