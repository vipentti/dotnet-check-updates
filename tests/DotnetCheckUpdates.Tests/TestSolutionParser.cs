// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;
using DotnetCheckUpdates.Core.ProjectModel;

namespace DotnetCheckUpdates.Tests;

internal sealed class TestSolutionParser : ISolutionParser
{
    private readonly IFileSystem _fileSystem;
    private readonly SolutionFileFormat _solutionFileFormat;

    public TestSolutionParser(
        IFileSystem fileSystem,
        SolutionFileFormat solutionFileFormat = SolutionFileFormat.Sln
    )
    {
        _fileSystem = fileSystem;
        _solutionFileFormat = solutionFileFormat;
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
        var tempSln = _solutionFileFormat switch
        {
            SolutionFileFormat.Slnx => Path.ChangeExtension(tempFile, ".slnx"),
            _ => Path.ChangeExtension(tempFile, ".sln"),
        };

        try
        {
            // Ensure the temp file has .sln extension
            if (!string.Equals(tempFile, tempSln, StringComparison.OrdinalIgnoreCase))
            {
                // Move the originally created temp file to the .sln path
                File.Move(tempFile, tempSln, overwrite: true);
            }

            var contents = _fileSystem.File.ReadAllText(solutionPath);

            // Write to actual FS
            File.WriteAllText(tempSln, contents);

            // path to the solution
            var solutionFilePath = _fileSystem.Path.GetDirectoryName(solutionPath);

            return
            [
                .. DefaultSolutionParser
                    .ParseProjectPathsFromSlnFile(tempSln)
                    .Select(it => it.Replace(tempDir, solutionFilePath)),
            ];
        }
        finally
        {
            try
            {
                if (File.Exists(tempSln))
                {
                    File.Delete(tempSln);
                }
                else if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
