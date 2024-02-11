// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.ProjectModel;
using Microsoft.Extensions.Logging;

namespace DotnetCheckUpdates.Core.Utils;

internal class ProjectDiscovery(
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
            projectFiles.AddRange(
                solutionProjectMap[request.Solution] = GetProjectFilePathsForSolution(
                    request.Solution
                )
            );
        }
        else
        {
            projectFiles.AddRange(
                await GetProjectFilesByPattern(request.Cwd, request.Recurse, request.Depth)
            );

            if (projectFiles.Count == 0)
            {
                foreach (
                    var (solution, solutionProjects) in await SolutionProjectsByPattern(request.Cwd)
                )
                {
                    projectFiles.AddRange(solutionProjectMap[solution] = solutionProjects);
                }
            }
        }

        var directoryBuildPropFiles = ImmutableHashSet.CreateBuilder<string>();

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(
                "Found {ProjectFiles} and solution keys {Solutions}",
                projectFiles.Count,
                string.Join(", ", solutionProjectMap.Keys)
            );
        }

        void LogAndAddPropsFile(string path)
        {
            if (directoryBuildPropFiles.Add(path))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("Found props file {File}", path);
                }
            }
        }

        // Discovering solutions
        if (solutionProjectMap.Count > 0)
        {
            var solutionSpecificDirectoryBuildProps = new Dictionary<string, string[]>();

            foreach (var (solution, solutionProjects) in solutionProjectMap)
            {
                directoryBuildPropFiles.Clear();

                var solutionDirectory = Path.GetDirectoryName(solution);

                {
                    if (
                        _fileFinder.TryGetPathOfFile(
                            CliConstants.DirectoryBuildPropsFileName,
                            solution,
                            out var path
                        )
                    )
                    {
                        LogAndAddPropsFile(path);
                    }
                }

                foreach (var project in solutionProjects)
                {
                    foreach (
                        var file in _fileFinder.GetFilesFromDirectoryAndAbove(
                            project,
                            CliConstants.DirectoryBuildPropsFileName
                        )
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

                if (directoryBuildPropFiles.Count > 0)
                {
                    solutionSpecificDirectoryBuildProps[solution] =
                    [
                        .. directoryBuildPropFiles.OrderBy(it => it, StringComparer.Ordinal)
                    ];
                }
            }

            foreach (var (solution, propsFiles) in solutionSpecificDirectoryBuildProps)
            {
                var originalProjects = solutionProjectMap[solution];
                solutionProjectMap[solution] =
                [
                    .. originalProjects.Concat(propsFiles).OrderBy(it => it, StringComparer.Ordinal)
                ];
                projectFiles.AddRange(propsFiles);
            }
        }
        else
        {
            foreach (var projectFile in projectFiles)
            {
                if (
                    _fileFinder.TryGetPathOfFile(
                        CliConstants.DirectoryBuildPropsFileName,
                        projectFile,
                        out var path
                    )
                )
                {
                    LogAndAddPropsFile(path);
                }
            }

            projectFiles.AddRange(directoryBuildPropFiles);
        }

        // TODO: This can be simplified once we drop support for older frameworks
#pragma warning disable IDE0305 // Simplify collection initialization
        return new(
            projectFiles.OrderBy(it => it, StringComparer.Ordinal).ToImmutableArray(),
            solutionProjectMap.ToImmutable()
        );
#pragma warning restore IDE0305 // Simplify collection initialization
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
                    cspattern = "*/" + cspattern;
                    fspattern = "*/" + fspattern;
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
