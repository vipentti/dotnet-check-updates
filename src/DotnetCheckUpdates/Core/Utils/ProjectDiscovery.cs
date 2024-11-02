// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.ProjectModel;
using Microsoft.Extensions.Logging;

namespace DotnetCheckUpdates.Core.Utils;

internal sealed partial class ProjectDiscovery(
    ILogger<ProjectDiscovery> logger,
    IFileFinder fileFinder,
    ISolutionParser solutionParser
)
{
    private readonly IFileFinder _fileFinder = fileFinder;
    private readonly ISolutionParser _solutionParser = solutionParser;

    internal record ProjectDiscoveryResult(
        ImmutableArray<string> ProjectFiles,
        ImmutableDictionary<string, string[]> SolutionProjectMap
    );

    internal record ProjectDiscoveryRequest
    {
        public string Cwd { get; init; } = "";
        public int Depth { get; init; }
        public bool Recurse { get; init; }
        public string? Project { get; init; }
        public string? Solution { get; init; }
    }

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Found {ProjectFiles} and solution keys {@Solutions}"
    )]
    private static partial void LogFoundProjectFiles(
        ILogger logger,
        int projectFiles,
        IEnumerable<string> solutions
    );

    [LoggerMessage(Level = LogLevel.Trace, Message = "Found props file {PropsFile}")]
    private static partial void LogFoundPropsFile(ILogger logger, string propsFile);

    public async Task<ProjectDiscoveryResult> DiscoverProjectsAndSolutions(
        ProjectDiscoveryRequest request
    )
    {
        var projectFiles = ImmutableArray.CreateBuilder<string>();
        var solutionProjectMap = ImmutableDictionary.CreateBuilder<string, string[]>();

        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            projectFiles.Add(request.Project);
        }
        else if (!string.IsNullOrWhiteSpace(request.Solution))
        {
            var items = solutionProjectMap[request.Solution] = GetProjectFilePathsForSolution(
                request.Solution
            );
            projectFiles.AddRange(items);
        }
        else
        {
            projectFiles.AddRange(
                await GetProjectFilesByPattern(request.Cwd, request.Recurse, request.Depth)
            );

#pragma warning disable S2583 // Conditionally executed code should be reachable
            if (projectFiles.Count == 0)
            {
                foreach (
                    var (solution, solutionProjects) in await SolutionProjectsByPattern(request.Cwd)
                )
                {
                    var items = solutionProjectMap[solution] = solutionProjects;
                    projectFiles.AddRange(items);
                }
            }
#pragma warning restore S2583 // Conditionally executed code should be reachable
        }

        LogFoundProjectFiles(logger, projectFiles.Count, solutionProjectMap.Keys);

        // Discovering solutions
        if (solutionProjectMap.Count > 0)
        {
            foreach (
                var (solution, propsFiles) in DiscoverPropsFiles(
                    logger,
                    solutionProjectMap,
                    CliConstants.DirectoryBuildPropsFileName
                )
            )
            {
                var originalProjects = solutionProjectMap[solution];
                solutionProjectMap[solution] =
                [
                    .. originalProjects
                        .Concat(propsFiles)
                        .OrderBy(it => it, StringComparer.Ordinal),
                ];
                projectFiles.AddRange(propsFiles);
            }

            foreach (
                var (solution, propsFiles) in DiscoverPropsFiles(
                    logger,
                    solutionProjectMap,
                    CliConstants.DirectoryPackagesPropsFileName
                )
            )
            {
                var originalProjects = solutionProjectMap[solution];
                solutionProjectMap[solution] =
                [
                    .. originalProjects
                        .Concat(propsFiles)
                        .OrderBy(it => it, StringComparer.Ordinal),
                ];
                projectFiles.AddRange(propsFiles);
            }
        }
        else
        {
            var propsFiles = ImmutableHashSet.CreateBuilder<string>();

            foreach (var projectFile in projectFiles)
            {
                if (
                    _fileFinder.TryGetPathOfFile(
                        CliConstants.DirectoryBuildPropsFileName,
                        projectFile,
                        out var path
                    ) && propsFiles.Add(path)
                )
                {
                    LogFoundPropsFile(logger, path);
                }
                else if (
                    _fileFinder.TryGetPathOfFile(
                        CliConstants.DirectoryPackagesPropsFileName,
                        projectFile,
                        out var path2
                    ) && propsFiles.Add(path2)
                )
                {
                    LogFoundPropsFile(logger, path2);
                }
            }

            projectFiles.AddRange(propsFiles);
        }

        // This can be simplified once we drop support for older frameworks
#pragma warning disable IDE0305 // Simplify collection initialization
        return new(
            projectFiles.OrderBy(it => it, StringComparer.Ordinal).ToImmutableArray(),
            solutionProjectMap.ToImmutable()
        );
#pragma warning restore IDE0305 // Simplify collection initialization
    }

    private Dictionary<string, string[]> DiscoverPropsFiles(
        ILogger<ProjectDiscovery> logger,
        ImmutableDictionary<string, string[]>.Builder solutionProjectMap,
        string propsFileName
    )
    {
        var solutionSpecificDirectoryBuildProps = new Dictionary<string, string[]>();

        var directoryBuildPropFiles = ImmutableHashSet.CreateBuilder<string>();

        void LogAndAddPropsFile(string path)
        {
            if (directoryBuildPropFiles.Add(path))
            {
                LogFoundPropsFile(logger, path);
            }
        }

        foreach (var (solution, solutionProjects) in solutionProjectMap)
        {
            directoryBuildPropFiles.Clear();

            var solutionDirectory = Path.GetDirectoryName(solution);

            if (_fileFinder.TryGetPathOfFile(propsFileName, solution, out var path))
            {
                LogAndAddPropsFile(path);
            }

            foreach (var project in solutionProjects)
            {
                foreach (
                    var file in _fileFinder.GetFilesFromDirectoryAndAbove(project, propsFileName)
                )
                {
                    var fileDirectory = Path.GetDirectoryName(file);

                    LogAndAddPropsFile(file);

                    if (solutionDirectory == fileDirectory)
                    {
                        break;
                    }
                }
            }

            // directoryBuildPropFiles will not be empty if directory build props files have been found
#pragma warning disable S2583 // Conditionally executed code should be reachable
            if (directoryBuildPropFiles.Count > 0)
            {
                solutionSpecificDirectoryBuildProps[solution] =
                [
                    .. directoryBuildPropFiles.OrderBy(it => it, StringComparer.Ordinal),
                ];
            }
#pragma warning restore S2583 // Conditionally executed code should be reachable
        }

        return solutionSpecificDirectoryBuildProps;
    }

    private async Task<IEnumerable<string>> GetProjectFilesByPattern(
        string cwd,
        bool recurse,
        int depth
    )
    {
        var cspattern = CliConstants.CsProjPattern;
        var fspattern = CliConstants.FsProjPattern;
        var patterns = new List<string> { cspattern, fspattern };

        if (recurse)
        {
            if (depth > 0)
            {
                // Limit depth of the search
                var maxDepth = Math.Min(depth, 16);
                for (var i = 0; i < maxDepth; ++i)
                {
#pragma warning disable S1643 // Strings should not be concatenated using '+' in a loop
                    cspattern = "*/" + cspattern;
                    fspattern = "*/" + fspattern;
#pragma warning restore S1643 // Strings should not be concatenated using '+' in a loop
                    patterns.Add(cspattern);
                    patterns.Add(fspattern);
                }
            }
            else
            {
                patterns[0] = "**/" + CliConstants.CsProjPattern;
                patterns[1] = "**/" + CliConstants.FsProjPattern;
            }
        }

        return await _fileFinder.GetMatchingPaths(cwd, patterns);
    }

    private async Task<List<(string Solution, string[] Projects)>> SolutionProjectsByPattern(
        string cwd
    )
    {
        var solutions = (await _fileFinder.GetMatchingPaths(cwd, CliConstants.SlnPattern)).ToList();
        return solutions.ConvertAll(sln => (sln, GetProjectFilePathsForSolution(sln)));
    }

    private string[] GetProjectFilePathsForSolution(string solutionPath)
    {
        var paths = _solutionParser.GetProjectPaths(solutionPath);
        return paths
            .Where(it =>
                it.EndsWith(CliConstants.CsProjExtensionWithDot, StringComparison.OrdinalIgnoreCase)
                || it.EndsWith(
                    CliConstants.FsProjExtensionWithDot,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToArray();
    }
}
