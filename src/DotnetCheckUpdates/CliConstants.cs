// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates;

internal static class CliConstants
{
    public const string CliName = "dotnet-check-updates";

    public const string JsonExtensionWithDot = ".json";

    public const string DirectoryBuildPropsFileName = "Directory.Build.props";

    public const string SlnExtensionWithoutDot = "sln";
    public const string SlnExtensionWithDot = "." + SlnExtensionWithoutDot;
    public const string SlnPattern = "*" + SlnExtensionWithDot;

    public const string CsProjExtensionWithoutDot = "csproj";
    public const string CsProjExtensionWithDot = "." + CsProjExtensionWithoutDot;
    public const string CsProjPattern = "*" + CsProjExtensionWithDot;

    public const string FsProjExtensionWithoutDot = "fsproj";
    public const string FsProjExtensionWithDot = "." + FsProjExtensionWithoutDot;
    public const string FsProjPattern = "*" + FsProjExtensionWithDot;
}
