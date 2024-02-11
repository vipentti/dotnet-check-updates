// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.Utils;

internal interface IFileFinder
{
    bool TryGetPathOfFile(string fileName, string cwd, out string filePath);
    string GetPathOfFileAbove(string fileName, string? startingDirectory = default);
    IEnumerable<string> GetFilesFromDirectoryAndAbove(string baseDirectory, string fileName);
    Task<IEnumerable<string>> GetMatchingPaths(string baseDirectory, IEnumerable<string> patterns);
    Task<IEnumerable<string>> GetMatchingPaths(string baseDirectory, string pattern) =>
        GetMatchingPaths(baseDirectory, new[] { pattern });
}
