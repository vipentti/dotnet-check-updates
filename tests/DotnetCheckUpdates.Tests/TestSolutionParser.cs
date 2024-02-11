// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;
using DotnetCheckUpdates.Core.ProjectModel;
using Nuke.Common.ProjectModel;

namespace DotnetCheckUpdates.Tests;

internal class TestSolutionParser : ISolutionParser
{
    private readonly IFileSystem _fileSystem;

    public TestSolutionParser(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public IEnumerable<string> GetProjectPaths(string solutionPath)
    {
        // This is a workaround because we utilize Nuke.Common.ProjectModel.SolutionModelTasks
        // for parsing the solution, which only supports reading from a file in the actual file system.
        //
        // So what we do is:
        //
        // 1. Read all the text from the mock filesystem
        // 2. Save the contents to an actual temp file in the actual file system
        // 3. Parse the solution using the SolutionModelTasks
        // 4. Fix the paths to point back to our mock filesystem

        var tempFile = Path.GetTempFileName();
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            var contents = _fileSystem.File.ReadAllText(solutionPath);

            // Write to actual FS
            File.WriteAllText(tempFile, contents);

            // path to the solution
            var solutionFilePath = _fileSystem.Path.GetDirectoryName(solutionPath);

            var solution = SolutionModelTasks.ParseSolution(tempFile);

            return solution
                .AllProjects.Select(it => it.Path.ToString().Replace(tempDir, solutionFilePath))
                .ToArray();
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // ignored
            }
        }
    }
}
