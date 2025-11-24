// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Spectre.Console;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal class DefaultSolutionParser : ISolutionParser
{
    public IEnumerable<string> GetProjectPaths(string solutionPath) =>
        ParseProjectPathsFromSlnFile(solutionPath);

    internal static IEnumerable<string> ParseProjectPathsFromSlnFile(string solutionPath)
    {
        var serializer =
            SolutionSerializers.GetSerializerByMoniker(solutionPath)
            ?? throw new InvalidOperationException("Invalid solution file");

        var solutionRootDirectory = Path.GetDirectoryName(solutionPath)!;

        var solutionModel = AsyncHelper.RunSync(
            async () => await serializer.OpenAsync(solutionPath, CancellationToken.None)
        );

        return solutionModel
            .SolutionProjects.Select(it =>
                Path.GetFullPath(Path.Combine(solutionRootDirectory, it.FilePath))
            )
            .ToArray();
    }
}
