
// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Spectre.Console;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    private async Task<(ImmutableArray<ProjectFile> Projects, ImmutableDictionary<string, string[]> Solutions)> DiscoverProjectsAndSolutions(string cwd, Settings settings)
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

        var projects = (await Task.WhenAll(projectFiles.Select(_projectReader.ReadProjectFile))).ToImmutableArray();
        var allSpecifiedTargetFrameworks = projects
            .SelectMany(it => it.TargetFrameworks)
            .Distinct()
            .ToImmutableArray();

        projects = projects.ConvertAll(it =>
        {
            var packages = ImmutableArray.CreateBuilder<PackageReference>(it.PackageCount);
            packages.AddRange(it.PackageReferences);

            if (includeFilters.Length > 0)
            {
                for (var i = packages.Count - 1; i >= 0; --i)
                {
                    var pkgName = packages[i].Name;
                    if (includeFilters.Any(it => it.IsMatch(pkgName)) is false)
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
                    if (excludeFilters.Any(it => it.IsMatch(pkgName)) is true)
                    {
                        packages.RemoveAt(i);
                    }
                }
            }

            return it with
            {
                PackageReferences = packages.ToImmutable()
            };
        });

        projects = projects.ConvertAll(it =>
        {
            // if we have no targetframeworks specified as part of the original project
            // for example because we are using Directory.Build.props files for common properties
            // We do best effort and use all frameworks available in any of the project files
            //
            // This is not 100% accurate, because we are not evaluating them like MSBuild does
            // but it should be good enough for most purposes
            if (it.TargetFrameworks.Length == 0)
            {
                LogFrameworkUpdated(
                    _logger,
                    it.FilePath,
                    it.PackageCount,
                    allSpecifiedTargetFrameworks
                );
                return it with { TargetFrameworks = allSpecifiedTargetFrameworks };
            }
            return it;
        });

        return (projects, solutionProjectMap);
    }
}
