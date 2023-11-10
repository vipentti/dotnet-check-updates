// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Nuke.Common.ProjectModel;
using Spectre.Console;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal class DefaultSolutionParser : ISolutionParser
{
    public IEnumerable<string> GetProjectPaths(string solutionPath)
    {
        var solution = SolutionModelTasks.ParseSolution(solutionPath);
        return solution.AllProjects.Select(it => it.Path.ToString());
    }
}
