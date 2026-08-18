// Copyright 2023-2026 Ville Penttinen
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
            var candidate = _fileSystem.Path.Combine(dir.FullName, fileName);
            if (_fileSystem.File.Exists(candidate))
            {
                filePath = _fileSystem.Path.GetFullPath(
                    ResolveExistingFileName(dir, fileName) ?? candidate
                );
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
                var candidate = _fileSystem.Path.Combine(cwd.FullName, fileName);
                if (_fileSystem.File.Exists(candidate))
                {
                    return _fileSystem.Path.GetFullPath(
                        ResolveExistingFileName(cwd, fileName) ?? candidate
                    );
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

    // File.Exists can match case-insensitively on case-insensitive filesystems, in
    // which case the candidate keeps the searched-for casing rather than the actual
    // on-disk name. Resolve the real directory entry so emitted paths preserve the
    // real casing and case-sensitive directories keep differently-cased variants of
    // the same name (e.g. Directory.Build.props vs directory.build.props) distinct.
    private static string? ResolveExistingFileName(IDirectoryInfo dir, string fileName)
    {
        string? caseInsensitiveMatch = null;

        foreach (var file in dir.GetFiles())
        {
            if (string.Equals(file.Name, fileName, StringComparison.Ordinal))
            {
                return file.FullName;
            }

            if (
                caseInsensitiveMatch is null
                && string.Equals(file.Name, fileName, StringComparison.OrdinalIgnoreCase)
            )
            {
                caseInsensitiveMatch = file.FullName;
            }
        }

        return caseInsensitiveMatch;
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
                    var candidate = _fileSystem.Path.Combine(cwd.FullName, actualFileName);
                    if (_fileSystem.File.Exists(candidate))
                    {
                        yield return _fileSystem.Path.GetFullPath(
                            ResolveExistingFileName(cwd, actualFileName) ?? candidate
                        );
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
