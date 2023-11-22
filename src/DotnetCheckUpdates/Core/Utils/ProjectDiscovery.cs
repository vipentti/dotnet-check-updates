// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.ProjectModel;

namespace DotnetCheckUpdates.Core.Utils;

internal class ProjectDiscovery
{
    private readonly IFileFinder _fileFinder;
    private readonly ISolutionParser _solutionParser;

    internal record ProjectDiscoveryResult(
        List<string> ProjectFiles,
        Dictionary<string, string[]> SolutionProjectMap
    );

    internal record ProjectDiscoveryRequest
    {
        public string Cwd { get; init; } = "";
        public int Depth { get; init; }
        public bool Recurse { get; init; }
        public string? Project { get; init; }
        public string? Solution { get; init; }
    }

    public ProjectDiscovery(IFileFinder fileFinder, ISolutionParser solutionParser)
    {
        _fileFinder = fileFinder;
        _solutionParser = solutionParser;
    }

    public async Task<ProjectDiscoveryResult> DiscoverProjectsAndSolutions(
        ProjectDiscoveryRequest request
    )
    {
        var projectFiles = new List<string>();
        var solutionProjectMap = new Dictionary<string, string[]>();

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
        return new(projectFiles, solutionProjectMap);
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
            .Where(
                it =>
                    it.EndsWith(
                        CliConstants.CsProjExtensionWithDot,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || it.EndsWith(
                        CliConstants.FsProjExtensionWithDot,
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .ToArray();
    }
}
