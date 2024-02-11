// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal record MockProject(string ProjectPath, string Framework = Frameworks.Default)
{
    public List<(string id, string version)> Packages { get; init; } = [];

    public string ToXml()
    {
        return ProjectFileUtils.ProjectFileXml(Packages, Framework);
    }
}
