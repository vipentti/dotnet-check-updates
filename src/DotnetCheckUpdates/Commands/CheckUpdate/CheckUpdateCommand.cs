// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using CliWrap;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Core.Utils;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
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
    private readonly ProjectDiscovery _projectDiscovery;
    private readonly ApplicationExitHandler? _exitHandler;
    private readonly CurrentDirectoryProvider? _currentDirectoryProvider;
    private readonly IPackageUpgradeServiceFactory _packageUpgradeServiceFactory;

    private PackageUpgradeService? _packageService;

    public CheckUpdateCommand(
        IAnsiConsole ansiConsole,
        ILogger<CheckUpdateCommand> logger,
        IFileSystem fileSystem,
        ProjectFileReader projectReader,
        IPackageUpgradeServiceFactory packageUpgradeServiceFactory,
        ProjectDiscovery projectDiscovery,
        CurrentDirectoryProvider? directoryProvider = default,
        ApplicationExitHandler? exitHandler = default
    )
    {
        _packageUpgradeServiceFactory = packageUpgradeServiceFactory;
        _ansiConsole = ansiConsole;
        _logger = logger;
        _fileSystem = fileSystem;
        _projectReader = projectReader;
        _exitHandler = exitHandler;
        _currentDirectoryProvider = directoryProvider;
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

    [LoggerMessage(Message = "Settings {@Settings}")]
    private static partial void LogSettings(ILogger logger, LogLevel level, object settings);

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "{Project} ({PackageCount}) updated to using TargetFrameworks {@Frameworks}"
    )]
    private static partial void LogFrameworkUpdated(
        ILogger logger,
        string project,
        int packageCount,
        ImmutableArray<NuGetFramework> frameworks
    );

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Found project {Project} with packages {PackageCount}"
    )]
    private static partial void LogProjectFound(ILogger logger, string project, int packageCount);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Solution project {SlnProject} not found in {ProjectType} projects"
    )]
    private static partial void LogSolutionProjectNotFound(
        ILogger logger,
        string slnProject,
        string projectType
    );

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var includeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Include);
        var excludeFilters = CheckUpdateCommandHelpers.SplitFilters(settings.Exclude);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogSettings(
                _logger,
                LogLevel.Debug,
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

        var cwd = _fileSystem.Path.GetFullPath(
            _fileSystem.Path.Combine(
                _fileSystem.Directory.GetCurrentDirectory(),
                settings.Cwd ?? _fileSystem.Directory.GetCurrentDirectory()
            )
        );

        if (_currentDirectoryProvider is not null)
        {
            _currentDirectoryProvider.CurrentDirectory = cwd;
        }

        if (settings.Interactive)
        {
            await ExecuteInteractiveUpgrade(cwd, settings, cancellationToken);
        }
        else
        {
            await ExecuteNonInteractiveUpgradeAsync(cwd, settings, cancellationToken);
        }

        return 0;
    }

    [MemberNotNull(nameof(_packageService))]
    private void InitializePackageService()
    {
        _packageService ??= _packageUpgradeServiceFactory.GetPackageUpgradeService();
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
