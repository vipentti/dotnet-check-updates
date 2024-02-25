// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

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

        var (projects, solutionProjectMap) = await DiscoverProjectsAndSolutions(cwd, settings);

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

        if (totalPackageCount == 0)
        {
            _ansiConsole.MarkupLine("");
            _ansiConsole.WriteLine(CommonStrings.NoPackagesMatchFilters);
            return;
        }

        var newProjects = await GetUpgradedProjects(settings, projects, cancellationToken);

        var upgradedProjects = new List<ProjectFile>();

        // Output the project package tree
        if (solutionProjectMap.Count > 0)
        {
            RenderSolutionUpgrades(
                new SolutionProjectRenderOptions()
                {
                    SolutionProjectMap = solutionProjectMap,
                    FormatPath = FormatPath,
                    OriginalProjects = projects,
                    NewProjects = newProjects,
                    Settings = settings,
                    HideIfNoUpgrade = false,
                },
                out upgradedProjects
            );
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
