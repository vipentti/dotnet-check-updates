// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using NuGet.Versioning;
using Spectre.Console;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    private sealed record SolutionProjectRenderOptions
    {
        public IReadOnlyDictionary<string, string[]> SolutionProjectMap { get; init; } = default!;

        public Func<string, string> FormatPath { get; init; } = default!;

        public ImmutableArray<ProjectFile> OriginalProjects { get; init; }

        public ImmutableArray<ProjectFile> NewProjects { get; init; }

        public Settings Settings { get; init; } = default!;

        public bool HideIfNoUpgrade { get; init; }
    }

    private void RenderSolutionUpgrades(
        SolutionProjectRenderOptions options,
        out List<ProjectFile> upgradedProjects
    )
    {
        upgradedProjects = [];

        var longestPackageNameLength = 0;
        var longestVersionLength = 0;

        foreach (var project in options.OriginalProjects)
        {
            foreach (var pkg in project.PackageReferences)
            {
                longestPackageNameLength = Math.Max(longestPackageNameLength, pkg.Name.Length);
                longestVersionLength = Math.Max(
                    longestVersionLength,
                    pkg.GetVersionString().Length
                );
            }
        }

        foreach (var (solutionFile, solutionProjectArray) in options.SolutionProjectMap)
        {
            var solutionRoot = new Tree(options.FormatPath(solutionFile));

            if (solutionProjectArray.Length == 0)
            {
                solutionRoot.AddNode("No projects found.");
            }

            var oldSlnProjects = new List<ProjectFile>(options.OriginalProjects.Length);
            var newSlnProjects = new List<ProjectFile>(options.OriginalProjects.Length);

            foreach (var slnProj in solutionProjectArray)
            {
                var foundOldProject = options.OriginalProjects.Find(it =>
                    string.Equals(it.FilePath, slnProj, StringComparison.Ordinal)
                );
                var foundNewProject = options.NewProjects.Find(it =>
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
                options.FormatPath,
                solutionRoot,
                oldSlnProjects,
                newSlnProjects,
                upgradedProjects,
                options.Settings,
                longestPackageNameLength,
                longestVersionLength,
                options.HideIfNoUpgrade
            );
            if (options.Settings.AsciiTree)
            {
                solutionRoot.Guide = TreeGuide.Ascii;
            }
            _ansiConsole.Write(solutionRoot);
        }
    }
}
