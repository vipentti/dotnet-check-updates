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
    private async Task ExecuteNonInteractiveUpgradeAsync(
        string cwd,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        string FormatPath(string path) =>
            !settings.ShowAbsolute
                ? path.Replace(cwd ?? "", "")
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar)
                : path;

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

        var projects = await Task.WhenAll(projectFiles.Select(_projectReader.ReadProjectFile));
        var allSpecifiedTargetFrameworks = projects
            .SelectMany(it => it.TargetFrameworks)
            .Distinct()
            .ToImmutableArray();

        var includeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Include);
        var excludeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Exclude);

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

        var hasSolutions = solutionProjectMap.Count > 0;

        var firstSolution = true;

        foreach (var (solutionFile, solutionProjects) in solutionProjectMap)
        {
            if (firstSolution)
            {
                firstSolution = false;
                _ansiConsole.MarkupLine("");
            }

            var solutionTree = new Tree(FormatPath(solutionFile));
            foreach (var proj in solutionProjects)
            {
                var text = FormatPath(proj);

                if (
                    settings.ShowPackageCount
                    && projects.Find(it =>
                        string.Equals(it.FilePath, proj, StringComparison.Ordinal)
                    )
                        is ProjectFile project
                )
                {
                    text += $" (packages: {project.PackageCount})";
                }

                solutionTree.AddNode(text);
            }
            if (settings.AsciiTree)
            {
                solutionTree.Guide = TreeGuide.Ascii;
            }
            _ansiConsole.Write(solutionTree);
        }

        Tree? projectsTree = null;

        if (!hasSolutions)
        {
            projectsTree = new Tree("Projects");
        }

        var longestPackageNameLength = 0;
        var longestVersionLength = 0;
        var totalPackageCount = 0;

        foreach (var project in projects)
        {
            projectsTree?.AddNode(FormatPath(project.FilePath));

            LogProjectFound(_logger, project.FilePath, project.PackageCount);

            foreach (var pkg in project.PackageReferences)
            {
                longestPackageNameLength = Math.Max(longestPackageNameLength, pkg.Name.Length);
                longestVersionLength = Math.Max(
                    longestVersionLength,
                    pkg.GetVersionString().Length
                );
            }

            totalPackageCount += project.PackageCount;
        }

        if (projectsTree is not null)
        {
            if (settings.AsciiTree)
            {
                projectsTree.Guide = TreeGuide.Ascii;
            }
            _ansiConsole.MarkupLine("");
            _ansiConsole.Write(projectsTree);
        }

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

        var upgradedProjects = new List<ProjectFile>();

        var newProjects = projectsWithPackages.ConvertAll(it =>
            it.project.UpdatePackageReferences(it.packages)
        );

        // Output the project package tree
        if (solutionProjectMap.Count > 0)
        {
            foreach (var (solutionFile, solutionProjectArray) in solutionProjectMap)
            {
                var solutionRoot = new Tree(FormatPath(solutionFile));

                if (solutionProjectArray.Length == 0)
                {
                    solutionRoot.AddNode("No projects found.");
                }

                var oldSlnProjects = new List<ProjectFile>(projects.Length);
                var newSlnProjects = new List<ProjectFile>(projects.Length);

                foreach (var slnProj in solutionProjectArray)
                {
                    var foundOldProject = projects.Find(it =>
                        string.Equals(it.FilePath, slnProj, StringComparison.Ordinal)
                    );
                    var foundNewProject = newProjects.Find(it =>
                        string.Equals(it.FilePath, slnProj, StringComparison.Ordinal)
                    );

                    if (foundOldProject is null)
                    {
                        LogSolutionProjectNotFound(_logger, slnProj, "original");
                    }

                    if (foundNewProject is null)
                    {
                        LogSolutionProjectNotFound(_logger, slnProj, "new");
                    }

                    if (foundOldProject is not null && foundNewProject is not null)
                    {
                        oldSlnProjects.Add(foundOldProject);
                        newSlnProjects.Add(foundNewProject);
                    }
                }

                CheckUpdateCommandHelpers.SetupGridInTree(
                    FormatPath,
                    solutionRoot,
                    oldSlnProjects,
                    newSlnProjects,
                    upgradedProjects,
                    settings,
                    longestPackageNameLength,
                    longestVersionLength
                );
                if (settings.AsciiTree)
                {
                    solutionRoot.Guide = TreeGuide.Ascii;
                }
                _ansiConsole.Write(solutionRoot);
            }
        }
        else
        {
            var root = new Tree("Projects");

            if (settings.AsciiTree)
            {
                root.Guide = TreeGuide.Ascii;
            }

            CheckUpdateCommandHelpers.SetupGridInTree(
                FormatPath,
                root,
                projects,
                newProjects,
                upgradedProjects,
                settings,
                longestPackageNameLength,
                longestVersionLength
            );
            _ansiConsole.Write(root);
        }

        // Output possible packge upgrades & restore
        if (settings.Upgrade && upgradedProjects.Count > 0)
        {
            _ansiConsole.MarkupLine("");
            foreach (var proj in upgradedProjects)
            {
                _ansiConsole.MarkupLine(
                    $"Upgrading packages in [yellow]{FormatPath(proj.FilePath)}[/]"
                );
                proj.Save(_fileSystem);
            }

            _ansiConsole.MarkupLine("");

            if (settings.Restore)
            {
                await RestorePackages(
                    solutionProjectMap,
                    hasSolutions,
                    upgradedProjects,
                    FormatPath
                );
            }
            else
            {
                _ansiConsole.MarkupLine("Run [blue]dotnet restore[/] to install new versions");
            }
            _ansiConsole.MarkupLine("");
        }

        // If not upgrading, output help text to run the upgrade command
        if (upgradedProjects.Count > 0 && !settings.Upgrade)
        {
            _ansiConsole.MarkupLine(settings.GetUpgradeCommandHelpText());
            _ansiConsole.MarkupLine("");
        }
    }
}
