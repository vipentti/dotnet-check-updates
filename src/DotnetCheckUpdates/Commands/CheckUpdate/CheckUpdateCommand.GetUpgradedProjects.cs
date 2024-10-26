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
        InitializePackageService();

        var totalPackageCount = projects.Select(it => it.PackageReferences.Length).Sum();

        if (settings.NoProgress)
        {
            return await Fetch(settings, projects, progressTask: null, cancellationToken);
        }

        return await _ansiConsole
            .Progress()
            .AutoClear(false)
            .HideCompleted(settings.HideProgressAfterComplete)
            .StartAsync(async ctx =>
            {
                // Define tasks
                var progressTask = ctx.AddTask(
                    $"[green]Fetching latest information for {totalPackageCount} packages[/]",
                    new() { AutoStart = true, MaxValue = totalPackageCount }
                );

                var results = await Fetch(settings, projects, progressTask, cancellationToken);

                Debug.Assert(ctx.IsFinished);

                return results;
            });

        async Task<
            ImmutableArray<(ProjectFile project, PackageUpgradeVersionDictionary packages)>
        > Fetch(
            Settings settings,
            ImmutableArray<ProjectFile> projects,
            ProgressTask? progressTask,
            CancellationToken cancellationToken
        )
        {
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
                        progress: progressTask,
                        settings.Concurrency,
                        settings.Target,
                        _logger,
                        cancellationToken
                    )
                );
            }

            return temp.MoveToImmutable();
        }
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
