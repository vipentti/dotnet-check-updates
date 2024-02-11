// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal record MockSolution(string SolutionPath)
{
    public List<MockProject> Projects { get; init; } = [];

    public string GetSolution()
    {
        return ProjectFileUtils.SolutionFile(
            Projects.Select(it =>
                (Path.GetFileNameWithoutExtension(it.ProjectPath), it.ProjectPath)
            )
        );
    }

    public MockFiles GetMockFiles(string cwd)
    {
        var solutionFilePath = cwd.PathCombine(SolutionPath);

        var solutionPath = Path.GetDirectoryName(solutionFilePath)!;

        var projectsWithPaths = Projects.ToDictionary(
            kvp => solutionPath.PathCombine(kvp.ProjectPath),
            kvp => kvp.ToXml()
        );

        return new MockFiles(projectsWithPaths) { [solutionFilePath] = GetSolution() };
    }
}
