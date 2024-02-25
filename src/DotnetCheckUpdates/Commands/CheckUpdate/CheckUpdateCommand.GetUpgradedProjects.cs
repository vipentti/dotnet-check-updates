// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Spectre.Console;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    private async Task<
        ImmutableArray<(ProjectFile project, PackageUpgradeVersionDictionary packages)>
    > GetProjectPackageUpgrades(
        Settings settings,
        ImmutableArray<ProjectFile> projects,
        CancellationToken cancellationToken
    )
    {
        var totalPackageCount = projects.Select(it => it.PackageReferences.Length).Sum();

        var projectsWithPackages = await _ansiConsole
            .Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .StartAsync(async ctx =>
            {
                // Define tasks
                var progressTask = ctx.AddTask(
                    $"[green]Fetching latest information for {totalPackageCount} packages[/]",
                    new() { AutoStart = true, MaxValue = totalPackageCount, }
                );

                var temp = ImmutableArray.CreateBuilder<(
                    ProjectFile project,
                    PackageUpgradeVersionDictionary packages
                )>(projects.Length);

                foreach (var proj in projects)
                {
                    temp.Add(
                        await CheckUpdateCommandHelpers.GetProjectPackageVersions(
                            proj,
                            _packageService,
                            progressTask,
                            settings.Concurrency,
                            settings.Target,
                            _logger,
                            cancellationToken
                        )
                    );
                }

                Debug.Assert(ctx.IsFinished);

                return temp.MoveToImmutable();
            });

        return projectsWithPackages;
    }

    private async Task<ImmutableArray<ProjectFile>> GetUpgradedProjects(
        Settings settings,
        ImmutableArray<ProjectFile> projects,
        CancellationToken cancellationToken
    )
    {
        var projectsWithPackages = await GetProjectPackageUpgrades(
            settings,
            projects,
            cancellationToken
        );

        return projectsWithPackages.ConvertAll(it =>
            it.project.UpdatePackageReferences(it.packages)
        );
    }
}
