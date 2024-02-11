// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using System.IO.Abstractions;
using CliWrap;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Core.Utils;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using ValidationResult = Spectre.Console.ValidationResult;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand : AsyncCommand<CheckUpdateCommand.Settings>
{
    private readonly IAnsiConsole _ansiConsole;
    private readonly ILogger<CheckUpdateCommand> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly ProjectFileReader _projectReader;
    private readonly PackageUpgradeService _packageService;
    private readonly ProjectDiscovery _projectDiscovery;
    private readonly ApplicationExitHandler? _exitHandler;

    public CheckUpdateCommand(
        IAnsiConsole ansiConsole,
        ILogger<CheckUpdateCommand> logger,
        IFileSystem fileSystem,
        ProjectFileReader projectReader,
        PackageUpgradeService packageService,
        ProjectDiscovery projectDiscovery,
        ApplicationExitHandler? exitHandler = default
    )
    {
        _ansiConsole = ansiConsole;
        _logger = logger;
        _fileSystem = fileSystem;
        _projectReader = projectReader;
        _packageService = packageService;
        _exitHandler = exitHandler;
        _projectDiscovery = projectDiscovery;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (
            !string.IsNullOrWhiteSpace(settings.Solution)
            && !_fileSystem.File.Exists(settings.Solution)
        )
        {
            return ValidationResult.Error($"Solution {settings.Solution} does not exist.");
        }

        if (
            !string.IsNullOrWhiteSpace(settings.Project)
            && !_fileSystem.File.Exists(settings.Project)
        )
        {
            return ValidationResult.Error($"Project {settings.Project} does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(settings.Cwd) && !_fileSystem.Directory.Exists(settings.Cwd))
        {
            return ValidationResult.Error($"Directory {settings.Cwd} does not exist.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var includeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Include);
        var excludeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Exclude);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Settings {@settings}",
                new
                {
                    settings.Upgrade,
                    settings.Recurse,
                    settings.Depth,
                    settings.Cwd,
                    Include = string.Join("|", includeFilters),
                    Exclude = string.Join("|", excludeFilters),
                }
            );
        }

        if (settings.ShowVersion)
        {
            var version = AssemblyUtils.GetEntryAssemblyVersion();
            _ansiConsole.MarkupLineInterpolated($"Version: [cyan]{version}[/]");
            return 0;
        }

        var cancellationToken = _exitHandler?.GracefulToken ?? CancellationToken.None;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Executing {command}", nameof(CheckUpdateCommand));
        }

        var cwd = Path.GetFullPath(
            Path.Combine(
                _fileSystem.Directory.GetCurrentDirectory(),
                settings.Cwd ?? _fileSystem.Directory.GetCurrentDirectory()
            )
        );

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
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(
                        "{Project} updated to using TargetFrameworks {@Frameworks}",
                        it.FilePath,
                        allSpecifiedTargetFrameworks
                    );
                }
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
                solutionTree.AddNode(FormatPath(proj));
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

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Found project {Project}", project.FilePath);
            }
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

                var oldSolutionProjects = projects
                    .Where(it => Array.IndexOf(solutionProjectArray, it.FilePath) >= 0)
                    .ToList();

                var newdSolutionProjects = newProjects
                    .Where(it => Array.IndexOf(solutionProjectArray, it.FilePath) >= 0)
                    .ToList();
                CheckUpdateCommandHelpers.SetupGridInTree(
                    FormatPath,
                    solutionRoot,
                    oldSolutionProjects,
                    newdSolutionProjects,
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

        return 0;
    }

    private async Task RestorePackages(
        IReadOnlyDictionary<string, string[]> solutionProjectMap,
        bool hasSolutions,
        List<ProjectFile> upgradedProjects,
        Func<string, string> formatPath
    )
    {
        _ansiConsole.MarkupLine("Restoring packages...");

        var dotnet = Cli.Wrap("dotnet")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(_ansiConsole.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(_ansiConsole.WriteLine));

        var cmds = new List<CliWrap.Command>(
            Math.Max(solutionProjectMap.Count, upgradedProjects.Count)
        );

        if (hasSolutions)
        {
            cmds.AddRange(
                solutionProjectMap.Select(it => dotnet.WithArguments(new[] { "restore", it.Key }))
            );
        }
        else
        {
            cmds.AddRange(
                upgradedProjects.Select(it =>
                    dotnet.WithArguments(new[] { "restore", it.FilePath })
                )
            );
        }

        foreach (var restoreCmd in cmds)
        {
            _ansiConsole.WriteLine(formatPath(restoreCmd.ToString()));
            await restoreCmd.ExecuteAsync();
        }
    }
}
