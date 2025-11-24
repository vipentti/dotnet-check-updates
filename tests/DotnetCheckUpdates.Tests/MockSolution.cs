// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal sealed record MockSolution(string SolutionPath, SolutionFileFormat SolutionFileFormat)
{
    public List<MockProject> Projects { get; init; } = [];

    public string GetSolution()
    {
        var items =
            Projects
                .Where(it =>
                    !it.ProjectPath.Contains(
                        CliConstants.DirectoryBuildPropsFileName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Select((it, index) => ($"{index}-{Path.GetFileNameWithoutExtension(it.ProjectPath)}", it.ProjectPath))
                .ToArray();

        return SolutionFileFormat switch
        {
            SolutionFileFormat.Slnx => ProjectFileUtils.SolutionFileXml(items),
            _ => ProjectFileUtils.SolutionFile(items),
        };
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
