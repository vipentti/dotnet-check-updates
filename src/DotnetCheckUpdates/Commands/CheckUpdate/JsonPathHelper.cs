// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal static class JsonPathHelper
{
    public static string Canonicalize(string path, IFileSystem fileSystem)
    {
        return fileSystem.Path.GetFullPath(path);
    }

    public static string CanonicalizeAgainstProcessWd(
        string path,
        string processWd,
        IFileSystem fileSystem
    )
    {
        if (fileSystem.Path.IsPathRooted(path))
        {
            return fileSystem.Path.GetFullPath(path);
        }

        return fileSystem.Path.GetFullPath(fileSystem.Path.Combine(processWd, path));
    }

    public static string ToJsonDisplayPath(
        string canonicalFullPath,
        string canonicalCwd,
        bool showAbsolute
    )
    {
        if (showAbsolute)
        {
            return canonicalFullPath;
        }

        var relative = Path.GetRelativePath(canonicalCwd, canonicalFullPath);
        return relative;
    }

    public static string GetKind(string filePath)
    {
        if (filePath.EndsWith(CliConstants.DirectoryBuildPropsFileName, StringComparison.Ordinal))
        {
            return JsonOutputKind.DirectoryBuildProps;
        }

        if (
            filePath.EndsWith(CliConstants.DirectoryPackagesPropsFileName, StringComparison.Ordinal)
        )
        {
            return JsonOutputKind.DirectoryPackagesProps;
        }

        return JsonOutputKind.Project;
    }
}
