// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Text.RegularExpressions;
using DotnetCheckUpdates.Core.Utils;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal partial record Import(string Project)
{
#if NET7_0_OR_GREATER
    private static readonly Regex s_GetPathOfFileAboveRE = GeneratePathofFileAboveRegex();
#else
    private static readonly Regex s_GetPathOfFileAboveRE =
        new(
            """::GetPathOfFileAbove\(\s*['"]\s*([\$\(\)\w\./]+)['"]\s*,\s*['""]\s*([\$\(\)\w\./]+)\s*['""]\s*\)""",
            RegexOptions.Compiled
        );
#endif

    public string GetImportedProjectPath(IFileFinder fileFinder, string thisFileDirectory)
    {
        //
        // Assume format
        // $([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))
        // "::GetPathOfFileAbove\(['""]([\w\.]+)['""]\s*,\s*['""]([\$\(\)\w\./]+)['""]\)"gm
        if (s_GetPathOfFileAboveRE.Match(Project) is Match match)
        {
            var fileName = match.Groups[1].Value;

            static string EnsureTrailingSeparator(string input)
            {
                var span = input.AsSpan();

                span = span.TrimEnd(Path.DirectorySeparatorChar)
                    .TrimEnd(Path.AltDirectorySeparatorChar);

                return span.ToString() + Path.DirectorySeparatorChar;
            }

            var startingDirectory = match
                .Groups[2]
                .Value.Replace(
                    "$(MSBuildThisFileDirectory)",
                    EnsureTrailingSeparator(thisFileDirectory)
                );

            return fileFinder.GetPathOfFileAbove(fileName, startingDirectory);
        }

        return Project;
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(
        """::GetPathOfFileAbove\(\s*['"]\s*([\$\(\)\w\./]+)['"]\s*,\s*['""]\s*([\$\(\)\w\./]+)\s*['""]\s*\)""",
        RegexOptions.Compiled
    )]
    private static partial Regex GeneratePathofFileAboveRegex();
#endif
}
