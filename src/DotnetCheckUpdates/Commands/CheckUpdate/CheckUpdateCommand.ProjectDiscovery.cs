// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Spectre.Console;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    private async Task<(
        ImmutableArray<ProjectFile> Projects,
        ImmutableDictionary<string, string[]> Solutions
    )> DiscoverProjectsAndSolutions(string cwd, Settings settings)
    {
        var (projectFiles, solutionProjectMap) =
            await _projectDiscovery.DiscoverProjectsAndSolutions(
                new()
                {
                    Cwd = cwd,
                    Recurse = settings.Recurse,
                    Depth = settings.Depth,
                    Project = settings.Project,
                    Solution = settings.Solution,
                }
            );

        var includeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Include);
        var excludeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Exclude);

        var projects = (
            await Task.WhenAll(projectFiles.Select(_projectReader.ReadProjectFile))
        ).ToImmutableArray();
        var allSpecifiedTargetFrameworks = projects
            .SelectMany(it => it.TargetFrameworks)
            .Distinct()
            .ToImmutableArray();

        projects = projects.ConvertAll(it =>
            CheckUpdateCommandHelpers.ApplyFilters(it, includeFilters, excludeFilters)
        );

        projects = projects.ConvertAll(it =>
        {
            var effective = CheckUpdateCommandHelpers.GetEffectiveFrameworks(
                it,
                allSpecifiedTargetFrameworks
            );
            if (it.TargetFrameworks.Length == 0 && effective.Length > 0)
            {
                LogFrameworkUpdated(_logger, it.FilePath, it.PackageCount, effective);
                return it with { TargetFrameworks = effective };
            }
            return it;
        });

        return (projects, solutionProjectMap);
    }
}
