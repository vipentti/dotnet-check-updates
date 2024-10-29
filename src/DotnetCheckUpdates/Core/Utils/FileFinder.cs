// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing;
using Vipentti.IO.Abstractions.FileSystemGlobbing;

namespace DotnetCheckUpdates.Core.Utils;

internal class FileFinder : IFileFinder
{
    private readonly IFileSystem _fileSystem;

    public FileFinder(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool TryGetPathOfFile(string fileName, string cwd, out string filePath)
    {
        filePath = "";

        // Absolute paths returned as is
        if (_fileSystem.Path.IsPathRooted(fileName))
        {
            if (_fileSystem.File.Exists(fileName))
            {
                filePath = fileName;
                return true;
            }

            return false;
        }

        fileName =
            _fileSystem.Path.GetFileName(fileName)
            ?? throw new InvalidOperationException("Must be have a filename");

        var dir = _fileSystem.DirectoryInfo.New(GetFullDirectoryPath(cwd));

        if (dir.Exists)
        {
            var file = dir.GetFiles(fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (file?.Exists is true)
            {
                filePath = file.FullName;
                return true;
            }
        }

        return false;
    }

    // https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions?view=vs-2022#msbuild-getpathoffileabove
    public string GetPathOfFileAbove(string fileName, string? startingDirectory = default)
    {
        // Absolute paths returned as is
        if (_fileSystem.Path.IsPathRooted(fileName))
        {
            if (_fileSystem.File.Exists(fileName))
            {
                return fileName;
            }

            return "";
        }

        fileName =
            _fileSystem.Path.GetFileName(fileName)
            ?? throw new InvalidOperationException("Must be have a filename");

        var cwd = _fileSystem.DirectoryInfo.New(GetFullDirectoryPath(startingDirectory ?? "./"));

        while (cwd is not null)
        {
            if (cwd.Exists)
            {
                foreach (var file in cwd.GetFiles(fileName, SearchOption.TopDirectoryOnly))
                {
                    if (file.Exists)
                    {
                        return file.FullName;
                    }
                }
            }

            cwd = cwd.Parent;
        }

        return "";
    }

    private string GetFullDirectoryPath(string dir)
    {
        return _fileSystem.Path.GetFullPath(
            _fileSystem.Path.GetDirectoryName(dir)
                ?? throw new InvalidOperationException("Must have directory"),
            _fileSystem.Directory.GetCurrentDirectory()
        );
    }

    public IEnumerable<string> GetFilesFromDirectoryAndAbove(string baseDirectory, string fileName)
    {
        // Absolute paths returned as is
        if (_fileSystem.Path.IsPathRooted(fileName))
        {
            yield return fileName;
            yield break;
        }

        var actualFileName =
            _fileSystem.Path.GetFileName(fileName)
            ?? throw new InvalidOperationException("Must be have a filename");

        baseDirectory = GetFullDirectoryPath(baseDirectory);

        HashSet<string> directoriesToSearch = [baseDirectory];

        foreach (var dir in directoriesToSearch)
        {
            foreach (var files in SearchForFileStartingFrom(dir))
            {
                yield return files;
            }
        }

        IEnumerable<string> SearchForFileStartingFrom(string directory)
        {
            var cwd = _fileSystem.DirectoryInfo.New(directory);

            while (cwd is not null)
            {
                if (cwd.Exists)
                {
                    foreach (
                        var file in cwd.GetFiles(actualFileName, SearchOption.TopDirectoryOnly)
                    )
                    {
                        if (file.Exists)
                        {
                            yield return file.FullName;
                        }
                    }
                }

                cwd = cwd.Parent;
            }
        }
    }

    public async Task<IEnumerable<string>> GetMatchingPaths(
        string baseDirectory,
        IEnumerable<string> patterns
    )
    {
        var matcher = new Matcher();
        matcher.AddIncludePatterns(patterns);

        var gitignoreFile = Path.GetFullPath(".gitignore", baseDirectory);

        if (_fileSystem.File.Exists(gitignoreFile))
        {
            var gitIgnoreGlobs = await _fileSystem.File.ReadAllLinesAsync(gitignoreFile);
            matcher.AddExcludePatterns(gitIgnoreGlobs);
        }

        return matcher.GetResultsInFullPath(_fileSystem, baseDirectory);
    }

    public Task<IEnumerable<string>> GetMatchingPaths(string baseDirectory, string pattern) =>
        GetMatchingPaths(baseDirectory, new[] { pattern });
}
