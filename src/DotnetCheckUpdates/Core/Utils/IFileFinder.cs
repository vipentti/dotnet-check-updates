// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.Utils;

internal interface IFileFinder
{
    Task<IEnumerable<string>> GetMatchingPaths(string baseDirectory, IEnumerable<string> patterns);
    Task<IEnumerable<string>> GetMatchingPaths(string baseDirectory, string pattern) =>
        GetMatchingPaths(baseDirectory, new[] { pattern });
}
