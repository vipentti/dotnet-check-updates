// Copyright 2023 Ville Penttinen
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
